﻿namespace HRPlatform.Common.Errors;

public class NotFoundException : Exception {
    public NotFoundException(string message) : base(message) { }
}
public class ConflictException : Exception {
    public ConflictException(string message) : base(message) { }
}