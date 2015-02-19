using System;
using System.Collections.Generic;
namespace KungFq
{
    public class QualityCounter : IQualityCounter
    {
        //public QualityCounter()
        //{
            //we preload all chars with 0 to avoid an if in all the Add executions.
            //we will trim all values with 0 counts at the extremes before giving
            //infos to other objects. No: TryGetValue solved the problem.
        //}
        
        Dictionary<char, int> count = new Dictionary<char, int>();
        //int[] qualities = new int[ ... could be done but will constrain more possible quality values
        
        public void Add(string quality) 
        {
            if (quality == null)
                return;
            ////if (qualities.ContainsKey(quality)) {
            //qualities[quality]++; //even without the if it is better to use TryGetValue? should be 2 lookups
            int value;
            for (int i = 0; i < quality.Length; i++) {
                char q = quality[i];
                if (count.TryGetValue(q, out value)) {
                    count[q] = value + 1;
                } else {
                    count[q] = 1;   
                }
            }
        }
        
        public IDictionary<char, int> QualityCounts 
        {
            get
            {
                return count;
            }
        }
    }
}

