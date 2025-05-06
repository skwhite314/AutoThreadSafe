namespace AutoThreadSafe.Exceptions
{
    public class AutoThreadSafeException : Exception
    {
        public AutoThreadSafeException(string message, Exception innerException) : base(message, innerException) { }

        public AutoThreadSafeException(string message) : base(message) { }

        public AutoThreadSafeException(Exception innerException) : this(innerException.Message, innerException) { }

        public AutoThreadSafeException() : base() { }
    }
}
