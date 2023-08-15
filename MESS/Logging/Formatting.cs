using System.Text;

namespace MESS.Logging
{
    internal class Formatting
    {
        public static string FormatException(Exception exception, bool stacktrace = true, bool data = true, bool innerExceptions = true)
        {
            var output = new StringBuilder();
            var ex = exception;
            while (ex != null)
            {
                output.AppendLine($"{ex.GetType().Name}: '{ex.Message}'.");

                if (stacktrace)
                {
                    output.AppendLine(ex.StackTrace);
                }

                if (data)
                {
                    foreach (var key in ex.Data.Keys)
                        output.AppendLine($"  {key}: \"{ex.Data[key]}\"");
                }

                if (innerExceptions)
                {
                    ex = ex.InnerException;
                    if (ex != null)
                        output.AppendLine("Inner exception:");
                }
                else
                {
                    break;
                }
            }
            return output.ToString();
        }
    }
}
