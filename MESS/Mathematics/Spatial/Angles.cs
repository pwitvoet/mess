
namespace MESS.Mathematics.Spatial
{
    /// <summary>
    /// Represents Euler rotation angles.
    /// While these are often written in pitch,yaw,roll order, the actual order in which the rotations are applied
    /// is roll,pitch,yaw, which corresponds to rotations around the x, y and z axis.
    /// </summary>
    public struct Angles
    {
        public float Roll;  // x-axis: Leaning over sidewards to the right (positive) or left (negative)
        public float Pitch; // y-axis: Looking down (positive) or up (negative) (NOTE: for studio models and light entities, this is inverted!)
        public float Yaw;   // z-axis: Turning left (positive) or right (negative)

        public Angles(float roll, float pitch, float yaw)
        {
            Roll = roll;
            Pitch = pitch;
            Yaw = yaw;
        }
    }
}
