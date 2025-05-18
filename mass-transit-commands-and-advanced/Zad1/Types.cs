using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zad1
{
    public class Publ
    {
        public int Number { get; set; }
    }

    public interface Response
    {
        string message { get; set; }
    }

    public class ResponseA : Response
    {
        public string message { get; set; }
    }

    public class ResponseB : Response
    {
        public string message { get; set; }
    }
}
