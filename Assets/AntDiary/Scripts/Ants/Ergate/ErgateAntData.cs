﻿using System.Collections;
using System.Collections.Generic;
using MessagePack;
using UnityEngine;

namespace AntDiary
{
    [MessagePackObject()]
    public class ErgateAntData : AntData
    {
        [Key(100)]
        public bool IsHoldingFood { get; set; } = false;

        [Key(101)]
        public int Capacity { get; set; }
    }
}