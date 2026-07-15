using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces.IBackgroundServices
{
    public interface IIngestBackgroundService
    {
        void Enqueue(Guid fileItemId);
    }
}
