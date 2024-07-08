using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DVT.AndreM.Elevator
{
    public static class HelperStatic
    {
        public static string FloorName(int? floorKey)
        {
            if(!floorKey.HasValue) return $"Invalid Floor";
            if (floorKey == 0) return $"Ground Floor";
            string name = (floorKey.Value < 0) ? "Basement" : "Floor";
            return $"{name}{Math.Abs(floorKey.Value)}";
        }
    }
}
