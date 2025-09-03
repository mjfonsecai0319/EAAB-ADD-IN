using System;

namespace EAABAddIn.Src.Application.Errors
{
    public class BusinessException : Exception
    {
        public BusinessException(string message) : base(message) { }
    }
}
