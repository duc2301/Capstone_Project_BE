using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces.IBackgroundServices
{
    public interface INameMatchContentBackgroundService
    {
        void Enqueue(Guid fileItemId);
    }
}
