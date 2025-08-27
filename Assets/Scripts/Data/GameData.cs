using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    // 角色数据结构
    [System.Serializable]
    public class PlayerCharacterInfo
    {
        public int CharacterId { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public int Role { get; set; }
        public int SkinId { get; set; }
        public int MapId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
    }

}