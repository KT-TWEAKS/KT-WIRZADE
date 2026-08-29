using System;
using KTWirzade.Shared.Tasks;

namespace KTWirzade.Shared.Exceptions
{
    public class ErrorHandlingException : Exception
    {
        public ErrorHandlingException(TaskAction.ExitCodeAction action, string message) => (Action, Message) = (action, message);
        public TaskAction.ExitCodeAction Action { get; set; }
        public new string Message { get; set; }
    }
}
