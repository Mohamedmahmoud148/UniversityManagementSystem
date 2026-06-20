using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Services;
using Xunit;

namespace UniversityManagementSystem.Tests
{
    /// <summary>
    /// Tests for AtomicStorageService — Section 7 (File Consistency).
    /// Verifies that orphaned files are deleted when DB operations fail.
    /// </summary>
    public class AtomicStorageServiceTests
    {
        private readonly Mock<IStorageService> _storageMock;
        private readonly AtomicStorageService _sut;

        public AtomicStorageServiceTests()
        {
            _storageMock = new Mock<IStorageService>();
            _sut = new AtomicStorageService(
                _storageMock.Object,
                NullLogger<AtomicStorageService>.Instance);
        }

        [Fact]
        public async Task UploadWithCompensation_DbSucceeds_ReturnsKey()
        {
            // Arrange
            var expectedKey = "lecture-recordings/test-file.mp3";
            _storageMock
                .Setup(s => s.UploadAsync(It.IsAny<Stream>(), "test.mp3", "audio/mpeg", "lecture-recordings"))
                .ReturnsAsync(expectedKey);

            string? capturedKey = null;

            // Act
            var result = await _sut.UploadWithCompensationAsync(
                Stream.Null, "test.mp3", "audio/mpeg", "lecture-recordings",
                async key => { capturedKey = key; await Task.CompletedTask; });

            // Assert
            Assert.Equal(expectedKey, result);
            Assert.Equal(expectedKey, capturedKey);
            _storageMock.Verify(s => s.DeleteAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task UploadWithCompensation_DbFails_DeletesUploadedFile()
        {
            // Arrange
            var uploadedKey = "lecture-recordings/orphan-file.mp3";
            _storageMock
                .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(uploadedKey);
            _storageMock
                .Setup(s => s.DeleteAsync(uploadedKey))
                .Returns(Task.CompletedTask);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _sut.UploadWithCompensationAsync(
                    Stream.Null, "file.mp3", "audio/mpeg", "lecture-recordings",
                    async _ => throw new InvalidOperationException("DB save failed")));

            // The compensation should have deleted the orphaned file
            _storageMock.Verify(s => s.DeleteAsync(uploadedKey), Times.Once);
            Assert.Equal("DB save failed", ex.Message);
        }

        [Fact]
        public async Task UploadWithCompensation_BothFail_OriginalExceptionPropagates()
        {
            // Arrange
            var uploadedKey = "lecture-recordings/problem-file.mp3";
            _storageMock
                .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(uploadedKey);
            _storageMock
                .Setup(s => s.DeleteAsync(uploadedKey))
                .ThrowsAsync(new Exception("Storage delete also failed"));

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _sut.UploadWithCompensationAsync(
                    Stream.Null, "file.mp3", "audio/mpeg", "lecture-recordings",
                    async _ => throw new InvalidOperationException("DB failed")));

            // Original DB exception propagates even if compensation fails
            Assert.Equal("DB failed", ex.Message);
        }
    }
}
