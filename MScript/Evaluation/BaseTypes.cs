using MScript.Evaluation.Types;

namespace MScript.Evaluation
{
    class BaseTypes
    {
        /// <summary>
        /// None represents the absence of a value.
        /// </summary>
        public static TypeDescriptor None { get; }

        /// <summary>
        /// A floating-point double-precision number.
        /// </summary>
        public static TypeDescriptor Number { get; }

        /// <summary>
        /// A fixed-length sequence of numbers.
        /// </summary>
        public static TypeDescriptor Vector { get; }

        /// <summary>
        /// A sequence of characters.
        /// </summary>
        public static TypeDescriptor String { get; }

        /// <summary>
        /// A function or method.
        /// </summary>
        public static TypeDescriptor Function { get; }


        static BaseTypes()
        {
            // TODO: Depending on how complex this will become, we may need to allow mutations during this initialization phase!

            // NOTE: Initialization order is important here (earlier types can be referenced by later types):
            None = CreateNoneTypeDescriptor();
            Number = CreateNumberTypeDescriptor();
            Function = CreateFunctionTypeDescriptor();
            Vector = CreateVectorTypeDescriptor();
            String = CreateStringTypeDescriptor();
        }


        private static TypeDescriptor CreateNoneTypeDescriptor()
        {
            return new TypeDescriptor(nameof(None));
        }

        private static TypeDescriptor CreateNumberTypeDescriptor()
        {
            return new TypeDescriptor(nameof(Number));
        }

        private static TypeDescriptor CreateFunctionTypeDescriptor()
        {
            return new TypeDescriptor(nameof(Function));
        }

        private static TypeDescriptor CreateVectorTypeDescriptor()
        {
            return new TypeDescriptor(nameof(Vector),
                new PropertyDescriptor("length", Number, obj => (obj as double[]).Length),

                // Position properties:
                new PropertyDescriptor("x", Number, obj => Operations.Index(obj as double[], 0)),
                new PropertyDescriptor("y", Number, obj => Operations.Index(obj as double[], 1)),
                new PropertyDescriptor("z", Number, obj => Operations.Index(obj as double[], 2)),

                // Angles properties:
                new PropertyDescriptor("pitch", Number, obj => Operations.Index(obj as double[], 0)),
                new PropertyDescriptor("yaw", Number, obj => Operations.Index(obj as double[], 1)),
                new PropertyDescriptor("roll", Number, obj => Operations.Index(obj as double[], 2)),

                // Color + brightness properties:
                new PropertyDescriptor("r", Number, obj => Operations.Index(obj as double[], 0)),
                new PropertyDescriptor("g", Number, obj => Operations.Index(obj as double[], 1)),
                new PropertyDescriptor("b", Number, obj => Operations.Index(obj as double[], 2)),
                new PropertyDescriptor("brightness", Number, obj => Operations.Index(obj as double[], 3))
            );
        }

        private static TypeDescriptor CreateStringTypeDescriptor()
        {
            return new TypeDescriptor(nameof(String),
                new PropertyDescriptor("length", Number, obj => (obj as string).Length)

                // TODO: Add useful string methods here!
            );
        }
    }
}
