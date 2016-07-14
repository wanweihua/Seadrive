using System;
using System.Threading;
using System.Threading.Tasks;
using Data_Abstraction_Layer.Transport.Models;

namespace Data_Abstraction_Layer.Transport
{
    public class TransportUnitOfWork : IDisposable
    {
        private readonly TransportContext _context = new TransportContext();
        private bool _disposed;

        #region Repositories
        private GenericRepository<ChunksReceived> _cacheEntryRepository;
        private GenericRepository<LocalServerIdSession> _fileSystemEntryRepository;
        private GenericRepository<VirtualFile> _virtualFileRepository; 
        #endregion

        #region Repository getters
        public GenericRepository<ChunksReceived> ChunksReceivedRepository
        {
            get { return _cacheEntryRepository ?? (_cacheEntryRepository = new GenericRepository<ChunksReceived>(_context)); }
        }

        public GenericRepository<LocalServerIdSession> LocalServerIdSessionRepository
        {
            get { return _fileSystemEntryRepository ?? (_fileSystemEntryRepository = new GenericRepository<LocalServerIdSession>(_context)); }
        }

        public GenericRepository<VirtualFile> VirtualFileRepository
        {
            get { return _virtualFileRepository ?? (_virtualFileRepository = new GenericRepository<VirtualFile>(_context)); }
        }

        #endregion
        public void Save()
        {
            _context.SaveChanges();
        }

        public Task<int> SaveAsync()
        {
            return _context.SaveChangesAsync();
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }

        #region IDisposable implementation
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _context.Dispose();
                }
            }
            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
