using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KTWirzade.Shared.Predicates
{
    interface IPredicate
    {
        public Task<bool> Evaluate();
    }
}
