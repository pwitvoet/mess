using MESS.Mapping;
using System;

namespace MESS.Macros
{
    /// <summary>
    /// <para>This marks a method that can process a macro entity. Entity handler methods must take 3 arguments:</para>
    /// The <see cref="Map"/> where the macro expansion contents will be inserted,
    /// the macro <see cref="Entity"/> that is to be processed and
    /// the current <see cref="InstantiationContext"/> which can be used for evaluating expressions and resolving templates.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    class EntityHandlerAttribute : Attribute
    {
        public string EntityClassName { get; }


        public EntityHandlerAttribute(string entityClassName)
        {
            EntityClassName = entityClassName;
        }
    }
}
