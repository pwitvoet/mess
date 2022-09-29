namespace MESS.Mathematics.Spatial
{
    class Tetrahedron
    {
        public Vector3D Vertex1;
        public Vector3D Vertex2;
        public Vector3D Vertex3;
        public Vector3D Vertex4;

        public Tetrahedron(Vector3D v1, Vector3D v2, Vector3D v3, Vector3D v4)
        {
            Vertex1 = v1;
            Vertex2 = v2;
            Vertex3 = v3;
            Vertex4 = v4;
        }
    }
}
