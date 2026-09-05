using System.Collections.Generic;
using UnityEngine;

public class DataManager
{
    
    public void SaveData()
    {

    }

    public void LoadData()
    {

    }
}

struct WorldSaveData
{ 
    public List<PlayerSaveData> playerdatas;
}

struct PlayerSaveData
{
    public uint userId;

    public Vector3 positon;
    public Vector3 rotation;

    public PlayerStatus playerstatus;

    public List<ItemData> itemList;
}

struct PlayerStatus
{
    public int hp;
    public int stamina;

    public int power;
    public int agility;
    public int intelligence;
}

struct ItemSaveData
{
    public int InstanceID;   // 고유 식별자 (해당 아이템 객체의 고유 ID)
    public int ItemTypeID;   // 아이템 종류 ID (데이터베이스 ID)
    public int X;            // 그리드 X
    public int Y;            // 그리드 Y
    public bool IsRotated;   // 회전 여부
}