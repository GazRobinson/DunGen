using UnityEngine;

namespace Extensions
{
    public static class MyExtensions
    {
        public static float Top(this Bounds bound)
        {
            return bound.max.z;
        }
        public static float Bottom(this Bounds bound)
        {
            return bound.min.z;
        }
        public static float Right(this Bounds bound)
        {
            return bound.max.x;
        }
        public static float Left(this Bounds bound)
        {
            return bound.min.x;
        }
        public static float Ceil(this Bounds bound)
        {
            return bound.max.y;
        }
        public static float Floor(this Bounds bound)
        {
            return bound.min.y;
        }
    }

}