namespace MESS.Mathematics.Spatial
{
    /// <summary>
    /// Represents Euler rotation angles.
    /// While these are often written in pitch,yaw,roll order, the actual order in which the rotations are applied
    /// is roll,pitch,yaw, which corresponds to rotations around the x, y and z axis.
    /// </summary>
    public struct Angles
    {
        public double Roll;  // x-axis: Leaning over sidewards to the right (positive) or left (negative)
        public double Pitch; // y-axis: Looking down (positive) or up (negative) (NOTE: for studio models and light entities, this is inverted!)
        public double Yaw;   // z-axis: Turning left (positive) or right (negative)

        public Angles(double roll, double pitch, double yaw)
        {
            Roll = roll;
            Pitch = pitch;
            Yaw = yaw;
        }
    }
}
