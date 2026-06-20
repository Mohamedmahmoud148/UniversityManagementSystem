using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Infrastructure.Services
{
    /// <summary>
    /// Wraps IStorageService with a compensating-action pattern.
    ///
    /// PROBLEM (Section 7 of Architecture Review):
    ///   Upload to R2 succeeds → DB save fails → orphaned file in R2 forever.
    ///
    /// SOLUTION:
    ///   AtomicStorageService.UploadWithCompensationAsync() tracks the storage key
    ///   and deletes it automatically if the provided DB action throws.
    ///
    /// USAGE:
    ///   var key = await atomicStorage.UploadWithCompensationAsync(
    ///       stream, fileName, contentType, "lecture-recordings",
    ///       async storageKey => {
    ///           entity.StoragePath = storageKey;
    ///           context.Set.Add(entity);
    ///           await context.SaveChangesAsync(); // if this throws → file auto-deleted
    ///       });
    /// </summary>
    public class AtomicStorageService(
        IStorageService storage,
        ILogger<AtomicStorageService> logger)
    {
        /// <summary>
        /// Uploads a file to storage, then executes the DB action.
        /// If the DB action throws, the uploaded file is deleted as a compensating action.
        /// </summary>
        /// <param name="stream">File content to upload.</param>
        /// <param name="fileName">Original file name.</param>
        /// <param name="contentType">MIME type.</param>
        /// <param name="folder">Storage folder / prefix.</param>
        /// <param name="dbAction">Action that persists the storage key to DB. Must be atomic.</param>
        /// <returns>The storage key on success.</returns>
        public async Task<string> UploadWithCompensationAsync(
            Stream stream,
            string fileName,
            string contentType,
            string folder,
            Func<string, Task> dbAction)
        {
            var storageKey = await storage.UploadAsync(stream, fileName, contentType, folder);
            logger.LogDebug("AtomicStorage: uploaded {Key}", storageKey);

            try
            {
                await dbAction(storageKey);
                logger.LogDebug("AtomicStorage: DB action succeeded for {Key}", storageKey);
                return storageKey;
            }
            catch (Exception ex)
            {
                // Compensating action: delete the uploaded file to avoid orphans
                logger.LogWarning(
                    ex,
                    "AtomicStorage: DB action failed for {Key} — deleting uploaded file as compensation.",
                    storageKey
                );

                try
                {
                    await storage.DeleteAsync(storageKey);
                    logger.LogInformation("AtomicStorage: compensation succeeded — deleted {Key}", storageKey);
                }
                catch (Exception deleteEx)
                {
                    // Log but don't swallow original exception
                    logger.LogError(
                        deleteEx,
                        "AtomicStorage: compensation FAILED — {Key} is now an orphan in storage. Manual cleanup required.",
                        storageKey
                    );
                }

                throw; // Re-throw original DB exception
            }
        }
    }
}
