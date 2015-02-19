using System;
using System.Collections.Generic;

namespace KungFq
{
    public interface IQualityCounter
    {
        /* Adds a quality value to the already seen ones */
        void Add(string quality);
        
    }
}

