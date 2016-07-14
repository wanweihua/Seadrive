using System;
using System.Threading;
using System.Threading.Tasks;
using Data_Abstraction_Layer.Deduplication.Models;

namespace Data_Abstraction_Layer.Deduplication
{
    public class UnitOfWork : IDisposable
    {
        private readonly DeduplicationContext _context = new DeduplicationContext();
        private bool _disposed;

        #region Repositories
        private GenericRepository<CacheEntries> _cacheEntryRepository;
        private GenericRepository<FileDirectory> _fileSystemEntryRepository;
        #endregion

        #region Repository getters
        public GenericRepository<CacheEntries> CacheEntryRepository
        {
            get { return _cacheEntryRepository ?? (_cacheEntryRepository = new GenericRepository<CacheEntries>(_context)); }
        }

        public GenericRepository<FileDirectory> FilesystemEntryRepository
        {
            get { return _fileSystemEntryRepository ?? (_fileSystemEntryRepository = new GenericRepository<FileDirectory>(_context)); }
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
