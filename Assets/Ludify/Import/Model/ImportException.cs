using System;

namespace Ludify.Import
{
    /// <summary>
    /// An import failure whose Message is safe and helpful to show to a teacher in the UI.
    /// </summary>
    public class ImportException : Exception
    {
        public ImportException(string message, Exception inner = null) : base(message, inner) { }
    }
}
