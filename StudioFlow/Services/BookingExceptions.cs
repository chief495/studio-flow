public sealed class ResourceNotFoundException(string message) : Exception(message);

public sealed class BookingConflictException(string message) : Exception(message);
