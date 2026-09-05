using System;
using System.Collections.Generic;
using System.IO;
using ProjectG.CharacterActions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectG.CharacterEditor
{
    /// <summary>
    /// Scene 뷰에서 히트 볼륨을 배치한 뒤 범용 Hitbox Action 에셋으로 굽는 제작 도구입니다.
    /// </summary>
    public sealed class HitboxActionAuthoringWindow : EditorWindow
    {
        private const string PreviewRootName = "[Project G] Action Hitbox Preview (Not Saved)";
        private const string DefaultOutputFolder = "Assets/@Scripts/Character/Data/Actions";

        [SerializeField] private Transform _origin;
        [SerializeField] private HitboxActionDefinition _sourceDefinition;
        [SerializeField] private PlayerActionSlot _playerSlot = PlayerActionSlot.None;
        [SerializeField, Min(0f)] private float _cooldown = 0.5f;
        [SerializeField, Min(0f)] private float _executionDelay = 0.2f;
        [SerializeField, Min(0f)] private float _damageMultiplier = 1f;
        [SerializeField, Min(0f)] private float _minimumDamage = 1f;
        [SerializeField] private bool _allowFriendlyFire;
        [SerializeField] private LayerMask _targetLayers = ~0;
        [SerializeField] private QueryTriggerInteraction _queryTriggerInteraction = QueryTriggerInteraction.Ignore;
        [SerializeField] private string _assetName = "NewHitboxAction";
        [SerializeField] private string _outputFolder = DefaultOutputFolder;
        [SerializeField] private bool _assignToController = true;
        [SerializeField] private GameObject _previewRoot;

        private Vector2 _scrollPosition;

        /// <summary>
        /// Project G 히트박스 액션 제작 창을 엽니다.
        /// </summary>
        [MenuItem("Tools/Project G/Action Hitbox Authoring")]
        public static void Open()
        {
            HitboxActionAuthoringWindow window = GetWindow<HitboxActionAuthoringWindow>();
            window.titleContent = new GUIContent("Action Hitbox");
            window.minSize = new Vector2(420f, 560f);
            window.Show();
        }

        /// <summary>
        /// 기존 에셋 값을 불러온 상태로 제작 창을 엽니다.
        /// </summary>
        public static void OpenFor(HitboxActionDefinition definition)
        {
            Open();
            HitboxActionAuthoringWindow window = GetWindow<HitboxActionAuthoringWindow>();
            window._sourceDefinition = definition;
            window.LoadSourceDefinition();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Action Hitbox");
            SceneView.duringSceneGui += DrawSceneVolumes;

            if (_origin == null)
                TryUseSelectionAsOrigin();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawSceneVolumes;
            DestroyPreviewRoot();
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.LabelField("Hitbox Action 제작", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Sphere, Box, Capsule 프리뷰를 Scene 뷰에서 W/E/R로 조정한 뒤 SO로 저장합니다. " +
                "Unity Physics에는 OverlapCylinder가 없으므로 실린더형 캐릭터 판정은 Capsule을 사용합니다.",
                MessageType.Info);

            DrawOriginSection();
            EditorGUILayout.Space(8f);
            DrawSourceSection();
            EditorGUILayout.Space(8f);
            DrawActionSettings();
            EditorGUILayout.Space(8f);
            DrawVolumeSection();
            EditorGUILayout.Space(8f);
            DrawAssetSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawOriginSection()
        {
            EditorGUILayout.LabelField("1. 기준 캐릭터", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            Transform nextOrigin = (Transform)EditorGUILayout.ObjectField(
                "Action Origin",
                _origin,
                typeof(Transform),
                true);
            if (EditorGUI.EndChangeCheck())
                SetOrigin(nextOrigin);

            if (GUILayout.Button("현재 선택 오브젝트를 Origin으로 사용"))
                TryUseSelectionAsOrigin();

            if (_origin == null)
            {
                EditorGUILayout.HelpBox(
                    "CharacterActionController가 붙은 캐릭터 루트를 선택하거나 Action Origin을 지정하세요.",
                    MessageType.Warning);
            }
        }

        private void DrawSourceSection()
        {
            EditorGUILayout.LabelField("2. 기존 데이터 불러오기 (선택)", EditorStyles.boldLabel);
            _sourceDefinition = (HitboxActionDefinition)EditorGUILayout.ObjectField(
                "Source Action",
                _sourceDefinition,
                typeof(HitboxActionDefinition),
                false);

            using (new EditorGUI.DisabledScope(_sourceDefinition == null))
            {
                if (GUILayout.Button("Source 값을 프리뷰로 불러오기"))
                    LoadSourceDefinition();
            }
        }

        private void DrawActionSettings()
        {
            EditorGUILayout.LabelField("3. 행동 설정", EditorStyles.boldLabel);
            _playerSlot = (PlayerActionSlot)EditorGUILayout.EnumPopup(
                "Player Slot (Optional)",
                _playerSlot);
            EditorGUILayout.HelpBox(
                "플레이어 입력으로 사용할 때만 슬롯을 고르세요. 몬스터 행동은 None으로 두면 됩니다.",
                MessageType.Info);
            _cooldown = Mathf.Max(0f, EditorGUILayout.FloatField("Cooldown", _cooldown));
            _executionDelay = Mathf.Max(0f, EditorGUILayout.FloatField("Execution Delay", _executionDelay));
            _damageMultiplier = Mathf.Max(0f, EditorGUILayout.FloatField("Damage Multiplier", _damageMultiplier));
            _minimumDamage = Mathf.Max(0f, EditorGUILayout.FloatField("Minimum Damage", _minimumDamage));
            _allowFriendlyFire = EditorGUILayout.Toggle("Allow Friendly Fire", _allowFriendlyFire);

            SerializedObject windowObject = new SerializedObject(this);
            windowObject.Update();
            EditorGUILayout.PropertyField(
                windowObject.FindProperty(nameof(_targetLayers)),
                new GUIContent("Target Layers"));
            windowObject.ApplyModifiedProperties();

            _queryTriggerInteraction = (QueryTriggerInteraction)EditorGUILayout.EnumPopup(
                "Query Trigger Interaction",
                _queryTriggerInteraction);
        }

        private void DrawVolumeSection()
        {
            EditorGUILayout.LabelField("4. Scene 히트 볼륨", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(_origin == null))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Sphere"))
                {
                    AddPreviewVolume(HitVolumeDefinition.CreateSphere(
                        new Vector3(0f, 1f, 1f),
                        1f));
                }

                if (GUILayout.Button("+ Box"))
                {
                    AddPreviewVolume(HitVolumeDefinition.CreateBox(
                        new Vector3(0f, 1f, 1f),
                        Vector3.zero,
                        new Vector3(0.75f, 1f, 0.75f)));
                }

                if (GUILayout.Button("+ Capsule"))
                {
                    AddPreviewVolume(HitVolumeDefinition.CreateCapsule(
                        new Vector3(0f, 1f, 1f),
                        Vector3.zero,
                        0.75f,
                        2.5f,
                        HitVolumeAxis.Y));
                }
            }

            Collider[] colliders = GetPreviewColliders();
            if (colliders.Length == 0)
            {
                EditorGUILayout.HelpBox("위 버튼으로 히트 볼륨을 하나 이상 추가하세요.", MessageType.None);
                return;
            }

            EditorGUILayout.HelpBox(
                "아래 항목을 선택하고 Scene 뷰의 Move, Rotate, Scale 도구로 조정하세요.",
                MessageType.None);

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(collider.gameObject.name, EditorStyles.miniButtonLeft))
                    {
                        Selection.activeGameObject = collider.gameObject;
                        SceneView.RepaintAll();
                    }

                    if (GUILayout.Button("삭제", EditorStyles.miniButtonRight, GUILayout.Width(54f)))
                    {
                        Undo.DestroyObjectImmediate(collider.gameObject);
                        SceneView.RepaintAll();
                    }
                }
            }

            if (GUILayout.Button("프리뷰 볼륨 전체 삭제"))
                ClearPreviewVolumes();
        }

        private void DrawAssetSection()
        {
            EditorGUILayout.LabelField("5. ScriptableObject 생성", EditorStyles.boldLabel);
            _assetName = EditorGUILayout.TextField("Asset Name", _assetName);
            _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);
            _assignToController = EditorGUILayout.Toggle(
                "생성 후 Controller에 등록",
                _assignToController);

            bool canCreate =
                _origin != null &&
                GetPreviewColliders().Length > 0 &&
                !string.IsNullOrWhiteSpace(_assetName);

            using (new EditorGUI.DisabledScope(!canCreate))
            {
                if (GUILayout.Button("Create Hitbox Action SO", GUILayout.Height(34f)))
                    CreateActionAsset();
            }
        }

        private void TryUseSelectionAsOrigin()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
                return;

            CharacterActionController controller = selected.GetComponentInParent<CharacterActionController>();
            SetOrigin(controller != null ? controller.transform : selected.transform);
            Repaint();
        }

        private void SetOrigin(Transform origin)
        {
            if (origin != null && EditorUtility.IsPersistent(origin))
            {
                Debug.LogWarning(
                    "Project 창의 프리팹 에셋이 아니라 Scene 또는 Prefab Stage의 캐릭터를 Origin으로 지정하세요.",
                    origin);
                _origin = null;
                return;
            }

            _origin = origin;
            if (_previewRoot == null)
                return;

            _previewRoot.transform.SetParent(_origin, false);
            _previewRoot.transform.localPosition = Vector3.zero;
            _previewRoot.transform.localRotation = Quaternion.identity;
            _previewRoot.transform.localScale = Vector3.one;
            SceneView.RepaintAll();
        }

        private void LoadSourceDefinition()
        {
            if (_sourceDefinition == null)
                return;

            _playerSlot = FindPlayerSlotForSource(_sourceDefinition);
            _cooldown = _sourceDefinition.Cooldown;
            _executionDelay = _sourceDefinition.ExecutionDelay;
            _damageMultiplier = _sourceDefinition.DamageMultiplier;
            _minimumDamage = _sourceDefinition.MinimumDamage;
            _allowFriendlyFire = _sourceDefinition.AllowFriendlyFire;
            _targetLayers = _sourceDefinition.TargetLayers;
            _queryTriggerInteraction = _sourceDefinition.QueryTriggerInteraction;
            _assetName = _sourceDefinition.name + "_Copy";

            ClearPreviewVolumes();
            for (int i = 0; i < _sourceDefinition.HitVolumeCount; i++)
                AddPreviewVolume(_sourceDefinition.GetHitVolume(i));

            Repaint();
            SceneView.RepaintAll();
        }

        private PlayerActionSlot FindPlayerSlotForSource(
            CharacterActionDefinition definition)
        {
            CharacterActionController controller = _origin != null
                ? _origin.GetComponentInParent<CharacterActionController>()
                : null;
            if (controller == null)
                return definition.LegacyPlayerSlot;

            SerializedObject serializedController = new SerializedObject(controller);
            SerializedProperty bindings = serializedController.FindProperty("_playerActionBindings");
            for (int i = 0; i < bindings.arraySize; i++)
            {
                SerializedProperty binding = bindings.GetArrayElementAtIndex(i);
                if (binding.FindPropertyRelative("_definition").objectReferenceValue != definition)
                    continue;

                return (PlayerActionSlot)binding.FindPropertyRelative("_slot").intValue;
            }

            return definition.LegacyPlayerSlot;
        }

        private void AddPreviewVolume(in HitVolumeDefinition volume)
        {
            if (_origin == null)
                return;

            EnsurePreviewRoot();

            GameObject preview = new GameObject(GetUniquePreviewName(volume.Shape))
            {
                hideFlags = HideFlags.DontSaveInEditor
            };
            Undo.RegisterCreatedObjectUndo(preview, "Create Action Hitbox Preview");
            preview.transform.SetParent(_previewRoot.transform, false);
            preview.transform.localPosition = volume.LocalCenter;
            preview.transform.localRotation = Quaternion.Euler(volume.LocalEulerAngles);

            switch (volume.Shape)
            {
                case HitVolumeShape.Box:
                {
                    BoxCollider box = preview.AddComponent<BoxCollider>();
                    box.size = Vector3.one;
                    box.isTrigger = true;
                    box.enabled = false;
                    preview.transform.localScale = volume.BoxHalfExtents * 2f;
                    break;
                }

                case HitVolumeShape.Capsule:
                {
                    CapsuleCollider capsule = preview.AddComponent<CapsuleCollider>();
                    capsule.radius = 0.5f;
                    capsule.height = 2f;
                    capsule.direction = (int)volume.CapsuleAxis;
                    capsule.isTrigger = true;
                    capsule.enabled = false;
                    preview.transform.localScale = GetCapsulePreviewScale(
                        volume.Radius,
                        volume.CapsuleHeight,
                        volume.CapsuleAxis);
                    break;
                }

                default:
                {
                    SphereCollider sphere = preview.AddComponent<SphereCollider>();
                    sphere.radius = 0.5f;
                    sphere.isTrigger = true;
                    sphere.enabled = false;
                    preview.transform.localScale = Vector3.one * (volume.Radius * 2f);
                    break;
                }
            }

            Selection.activeGameObject = preview;
            SceneView.RepaintAll();
        }

        private void EnsurePreviewRoot()
        {
            if (_previewRoot != null)
                return;

            _previewRoot = new GameObject(PreviewRootName)
            {
                hideFlags = HideFlags.DontSaveInEditor
            };
            _previewRoot.transform.SetParent(_origin, false);
            _previewRoot.transform.localPosition = Vector3.zero;
            _previewRoot.transform.localRotation = Quaternion.identity;
            _previewRoot.transform.localScale = Vector3.one;
        }

        private void ClearPreviewVolumes()
        {
            if (_previewRoot == null)
                return;

            for (int i = _previewRoot.transform.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(_previewRoot.transform.GetChild(i).gameObject);

            SceneView.RepaintAll();
        }

        private void DestroyPreviewRoot()
        {
            if (_previewRoot == null)
                return;

            DestroyImmediate(_previewRoot);
            _previewRoot = null;
        }

        private Collider[] GetPreviewColliders()
        {
            return _previewRoot != null
                ? _previewRoot.GetComponentsInChildren<Collider>(true)
                : Array.Empty<Collider>();
        }

        private void CreateActionAsset()
        {
            List<HitVolumeDefinition> volumes = CollectPreviewVolumes();
            if (volumes.Count == 0)
                return;

            string folder = NormalizeAssetFolder(_outputFolder);
            if (folder == null)
            {
                Debug.LogError("Output Folder는 Assets 폴더 안의 경로여야 합니다.");
                return;
            }

            EnsureAssetFolder(folder);
            string safeName = MakeSafeFileName(_assetName);
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{safeName}.asset");

            HitboxActionDefinition definition = CreateInstance<HitboxActionDefinition>();
            SerializedObject serializedDefinition = new SerializedObject(definition);
            serializedDefinition.Update();
            serializedDefinition.FindProperty("_cooldown").floatValue = _cooldown;
            serializedDefinition.FindProperty("_executionDelay").floatValue = _executionDelay;
            serializedDefinition.FindProperty("_damageMultiplier").floatValue = _damageMultiplier;
            serializedDefinition.FindProperty("_minimumDamage").floatValue = _minimumDamage;
            serializedDefinition.FindProperty("_allowFriendlyFire").boolValue = _allowFriendlyFire;
            serializedDefinition.FindProperty("_targetLayers").intValue = _targetLayers.value;
            serializedDefinition.FindProperty("_queryTriggerInteraction").enumValueIndex =
                (int)_queryTriggerInteraction;

            SerializedProperty volumeArray = serializedDefinition.FindProperty("_hitVolumes");
            volumeArray.arraySize = volumes.Count;
            for (int i = 0; i < volumes.Count; i++)
            {
                HitVolumeDefinition volume = volumes[i];
                WriteVolume(volumeArray.GetArrayElementAtIndex(i), in volume);
            }

            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(definition, assetPath);

            if (_assignToController)
                AssignToOriginController(definition);

            AssetDatabase.SaveAssets();
            EditorUtility.SetDirty(definition);
            Selection.activeObject = definition;
            EditorGUIUtility.PingObject(definition);
            ShowNotification(new GUIContent($"생성 완료: {definition.name}"));
        }

        private List<HitVolumeDefinition> CollectPreviewVolumes()
        {
            Collider[] colliders = GetPreviewColliders();
            List<HitVolumeDefinition> volumes = new List<HitVolumeDefinition>(colliders.Length);

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                Transform preview = collider.transform;
                Vector3 colliderCenter = GetColliderCenter(collider);
                Vector3 localCenter = _previewRoot.transform.InverseTransformPoint(
                    preview.TransformPoint(colliderCenter));
                Quaternion localRotation = Quaternion.Inverse(_previewRoot.transform.rotation) * preview.rotation;
                Vector3 localScale = Abs(preview.localScale);

                if (collider is BoxCollider box)
                {
                    volumes.Add(HitVolumeDefinition.CreateBox(
                        localCenter,
                        localRotation.eulerAngles,
                        Vector3.Scale(box.size, localScale) * 0.5f));
                }
                else if (collider is CapsuleCollider capsule)
                {
                    HitVolumeAxis axis = (HitVolumeAxis)capsule.direction;
                    GetCapsuleScale(localScale, axis, out float axisScale, out float radiusScale);
                    float radius = capsule.radius * radiusScale;
                    float height = Mathf.Max(radius * 2f, capsule.height * axisScale);
                    volumes.Add(HitVolumeDefinition.CreateCapsule(
                        localCenter,
                        localRotation.eulerAngles,
                        radius,
                        height,
                        axis));
                }
                else if (collider is SphereCollider sphere)
                {
                    float radiusScale = Mathf.Max(localScale.x, Mathf.Max(localScale.y, localScale.z));
                    volumes.Add(HitVolumeDefinition.CreateSphere(
                        localCenter,
                        sphere.radius * radiusScale,
                        localRotation.eulerAngles));
                }
            }

            return volumes;
        }

        private void AssignToOriginController(HitboxActionDefinition definition)
        {
            CharacterActionController controller = _origin.GetComponentInParent<CharacterActionController>();
            if (controller == null)
            {
                Debug.LogWarning(
                    "Origin에서 CharacterActionController를 찾지 못해 SO만 생성했습니다.",
                    _origin);
                return;
            }

            Undo.RecordObject(controller, "Assign Hitbox Action Definition");
            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.Update();
            SerializedProperty actions = serializedController.FindProperty("_allowedActions");
            bool isAlreadyAllowed = false;

            for (int i = 0; i < actions.arraySize; i++)
            {
                CharacterActionDefinition existing =
                    actions.GetArrayElementAtIndex(i).objectReferenceValue as CharacterActionDefinition;
                if (existing == definition)
                {
                    isAlreadyAllowed = true;
                    break;
                }
            }

            if (!isAlreadyAllowed)
            {
                int targetIndex = actions.arraySize;
                actions.arraySize++;
                actions.GetArrayElementAtIndex(targetIndex).objectReferenceValue = definition;
            }

            AssignPlayerSlotBinding(serializedController, definition);
            serializedController.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);
        }

        private void AssignPlayerSlotBinding(
            SerializedObject serializedController,
            CharacterActionDefinition definition)
        {
            if (_playerSlot == PlayerActionSlot.None)
                return;

            SerializedProperty bindings = serializedController.FindProperty("_playerActionBindings");
            int targetIndex = -1;
            for (int i = 0; i < bindings.arraySize; i++)
            {
                SerializedProperty binding = bindings.GetArrayElementAtIndex(i);
                if (binding.FindPropertyRelative("_slot").intValue != (int)_playerSlot)
                    continue;

                targetIndex = i;
                break;
            }

            if (targetIndex < 0)
            {
                targetIndex = bindings.arraySize;
                bindings.arraySize++;
            }

            SerializedProperty targetBinding = bindings.GetArrayElementAtIndex(targetIndex);
            targetBinding.FindPropertyRelative("_slot").intValue = (int)_playerSlot;
            targetBinding.FindPropertyRelative("_definition").objectReferenceValue = definition;
        }

        private void DrawSceneVolumes(SceneView sceneView)
        {
            Collider[] colliders = GetPreviewColliders();
            if (colliders.Length == 0 || Event.current.type != EventType.Repaint)
                return;

            Matrix4x4 previousMatrix = Handles.matrix;
            Color previousColor = Handles.color;
            CompareFunction previousZTest = Handles.zTest;
            Handles.zTest = CompareFunction.LessEqual;

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                bool selected = Selection.activeGameObject == collider.gameObject;
                Handles.color = selected
                    ? new Color(1f, 0.25f, 0.05f, 1f)
                    : new Color(1f, 0.55f, 0.1f, 0.8f);

                if (collider is BoxCollider box)
                {
                    Handles.matrix = collider.transform.localToWorldMatrix;
                    Handles.DrawWireCube(box.center, box.size);
                }
                else if (collider is SphereCollider sphere)
                {
                    Handles.matrix = Matrix4x4.identity;
                    Vector3 center = sphere.transform.TransformPoint(sphere.center);
                    Vector3 scale = Abs(sphere.transform.lossyScale);
                    float radius = sphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
                    DrawWireSphere(center, radius);
                }
                else if (collider is CapsuleCollider capsule)
                {
                    Handles.matrix = Matrix4x4.identity;
                    DrawWireCapsule(capsule);
                }
            }

            Handles.matrix = previousMatrix;
            Handles.color = previousColor;
            Handles.zTest = previousZTest;
        }

        private static void DrawWireSphere(Vector3 center, float radius)
        {
            Handles.DrawWireDisc(center, Vector3.right, radius);
            Handles.DrawWireDisc(center, Vector3.up, radius);
            Handles.DrawWireDisc(center, Vector3.forward, radius);
        }

        private static void DrawWireCapsule(CapsuleCollider capsule)
        {
            Transform capsuleTransform = capsule.transform;
            Vector3 scale = Abs(capsuleTransform.lossyScale);
            HitVolumeAxis axis = (HitVolumeAxis)capsule.direction;
            GetCapsuleScale(scale, axis, out float axisScale, out float radiusScale);

            Vector3 localAxis = axis switch
            {
                HitVolumeAxis.X => Vector3.right,
                HitVolumeAxis.Z => Vector3.forward,
                _ => Vector3.up
            };
            Vector3 worldAxis = capsuleTransform.TransformDirection(localAxis).normalized;
            Vector3 center = capsuleTransform.TransformPoint(capsule.center);
            float radius = capsule.radius * radiusScale;
            float height = Mathf.Max(radius * 2f, capsule.height * axisScale);
            float halfSegment = Mathf.Max(0f, height * 0.5f - radius);
            Vector3 pointA = center - worldAxis * halfSegment;
            Vector3 pointB = center + worldAxis * halfSegment;

            Vector3 tangent = Vector3.Cross(worldAxis, Vector3.up);
            if (tangent.sqrMagnitude < 0.001f)
                tangent = Vector3.Cross(worldAxis, Vector3.right);
            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(worldAxis, tangent).normalized;

            Handles.DrawWireDisc(pointA, worldAxis, radius);
            Handles.DrawWireDisc(pointB, worldAxis, radius);
            Handles.DrawLine(pointA + tangent * radius, pointB + tangent * radius);
            Handles.DrawLine(pointA - tangent * radius, pointB - tangent * radius);
            Handles.DrawLine(pointA + bitangent * radius, pointB + bitangent * radius);
            Handles.DrawLine(pointA - bitangent * radius, pointB - bitangent * radius);
            DrawWireSphere(pointA, radius);
            DrawWireSphere(pointB, radius);
        }

        private static void WriteVolume(SerializedProperty property, in HitVolumeDefinition volume)
        {
            property.FindPropertyRelative("_shape").enumValueIndex = (int)volume.Shape;
            property.FindPropertyRelative("_localCenter").vector3Value = volume.LocalCenter;
            property.FindPropertyRelative("_localEulerAngles").vector3Value = volume.LocalEulerAngles;
            property.FindPropertyRelative("_boxHalfExtents").vector3Value = volume.BoxHalfExtents;
            property.FindPropertyRelative("_radius").floatValue = volume.Radius;
            property.FindPropertyRelative("_capsuleHeight").floatValue = volume.CapsuleHeight;
            property.FindPropertyRelative("_capsuleAxis").enumValueIndex = (int)volume.CapsuleAxis;
        }

        private static Vector3 GetColliderCenter(Collider collider)
        {
            return collider switch
            {
                BoxCollider box => box.center,
                SphereCollider sphere => sphere.center,
                CapsuleCollider capsule => capsule.center,
                _ => Vector3.zero
            };
        }

        private static Vector3 GetCapsulePreviewScale(float radius, float height, HitVolumeAxis axis)
        {
            float diameter = Mathf.Max(0.02f, radius * 2f);
            float axisScale = Mathf.Max(diameter, height) * 0.5f;
            return axis switch
            {
                HitVolumeAxis.X => new Vector3(axisScale, diameter, diameter),
                HitVolumeAxis.Z => new Vector3(diameter, diameter, axisScale),
                _ => new Vector3(diameter, axisScale, diameter)
            };
        }

        private static void GetCapsuleScale(
            Vector3 scale,
            HitVolumeAxis axis,
            out float axisScale,
            out float radiusScale)
        {
            switch (axis)
            {
                case HitVolumeAxis.X:
                    axisScale = scale.x;
                    radiusScale = Mathf.Max(scale.y, scale.z);
                    break;

                case HitVolumeAxis.Z:
                    axisScale = scale.z;
                    radiusScale = Mathf.Max(scale.x, scale.y);
                    break;

                default:
                    axisScale = scale.y;
                    radiusScale = Mathf.Max(scale.x, scale.z);
                    break;
            }
        }

        private string GetUniquePreviewName(HitVolumeShape shape)
        {
            string prefix = $"Hit Volume - {shape}";
            int index = 1;
            string candidate;

            do
            {
                candidate = $"{prefix} {index++}";
            }
            while (_previewRoot != null && _previewRoot.transform.Find(candidate) != null);

            return candidate;
        }

        private static string NormalizeAssetFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                return null;

            string normalized = folder.Replace('\\', '/').TrimEnd('/');
            return normalized == "Assets" || normalized.StartsWith("Assets/", StringComparison.Ordinal)
                ? normalized
                : null;
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string[] segments = folder.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }

        private static string MakeSafeFileName(string fileName)
        {
            string safeName = fileName.Trim();
            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalidCharacters.Length; i++)
                safeName = safeName.Replace(invalidCharacters[i], '_');

            return string.IsNullOrWhiteSpace(safeName) ? "NewHitboxAction" : safeName;
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }
    }

    /// <summary>
    /// Hitbox Action 인스펙터에서 Scene 제작 도구를 바로 열 수 있게 합니다.
    /// </summary>
    [CustomEditor(typeof(HitboxActionDefinition), true)]
    public sealed class HitboxActionDefinitionEditor : Editor
    {
        /// <inheritdoc />
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space(8f);

            if (GUILayout.Button("Open Scene Hitbox Authoring Tool", GUILayout.Height(28f)))
            {
                HitboxActionAuthoringWindow.OpenFor((HitboxActionDefinition)target);
            }
        }
    }
}
