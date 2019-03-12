using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ISC.iNet.DS.Services
{
    public interface IService
    {
        string Name { get; }

        void Start();
    }
}
