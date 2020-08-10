
namespace MESS.Mathematics.Spatial
{
    class Triangle
    {
        public Vector3D Vertex1;
        public Vector3D Vertex2;
        public Vector3D Vertex3;

        public Triangle(Vector3D v1, Vector3D v2, Vector3D v3)
        {
            Vertex1 = v1;
            Vertex2 = v2;
            Vertex3 = v3;
        }
    }
}
