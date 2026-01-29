using System.Collections.Concurrent;
using BitrixChecker.Models;

namespace BitrixChecker.Services
{
    public class LinkBufferService
    {
        private readonly ConcurrentQueue<CheckedLink> _queue = new();

        public void Enqueue(CheckedLink link)
        {
            _queue.Enqueue(link);
        }

        public List<CheckedLink> DequeueChunk(int chunkSize)
        {
            var list = new List<CheckedLink>();
            for (int i = 0; i < chunkSize; i++)
            {
                if (_queue.TryDequeue(out var link)) list.Add(link);
                else break;
            }
            return list;
        }

        public int Count => _queue.Count;
    }
}