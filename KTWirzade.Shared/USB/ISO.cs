using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Core;
using DiscUtils;
using DiscUtils.Compression;
using DiscUtils.Iso9660;
using DiscUtils.Streams;
using DiscUtils.Udf;
using DiscUtils.Vfs;
using Interprocess;
using JetBrains.Annotations;
using Microsoft.Win32.SafeHandles;
using Polly;
using Polly.Retry;
using static iso_mode.Win32;
using FileAccess = System.IO.FileAccess;
using FileMode = System.IO.FileMode;
using FileShare = System.IO.FileShare;

namespace iso_mode
{
    public static class ISO
    {
        private static Stream GetFileReadStream(FileStream fileStream)
        {
            Stream readStream;

            try
            {
                var headerBytes = new byte[4];

                fileStream.Read(headerBytes, 0, 4);
                fileStream.Seek(0, SeekOrigin.Begin);

                if (BitConverter.ToUInt16(headerBytes, 0) == 0x8b1f)
                {
                    byte[] fileSizeBytes = new byte[4];

                    fileStream.Seek(-4, SeekOrigin.End);
                    fileStream.Read(fileSizeBytes, 0, 4);
                    fileStream.Seek(0, SeekOrigin.Begin);

                    readStream = new GZipStream(fileStream, CompressionMode.Decompress, true);

                    int firstByte = -1;
                    try
                    {
                        firstByte = readStream.ReadByte();
                    }
                    catch { }

                    readStream.Dispose();
                    fileStream.Seek(0, SeekOrigin.Begin);
                    if (firstByte == -1)
                    {
                        throw new FileFormatException();
                    }
                    else
                    {
                        readStream = new GZipStream(fileStream, CompressionMode.Decompress, true);
                    }
                }
                else
                    throw new FileFormatException();
            }
            catch (Exception e)
            {
                try
                {
                    readStream = new BZip2.BZip2InputStream(fileStream) { IsStreamOwner = false };

                    int firstByte = -1;
                    try
                    {
                        firstByte = readStream.ReadByte();
                    }
                    catch { }

                    readStream.Dispose();
                    fileStream.Seek(0, SeekOrigin.Begin);
                    if (firstByte == -1)
                    {
                        throw new FileFormatException();
                    }
                    else
                    {
                        readStream = new BZip2.BZip2InputStream(fileStream) { IsStreamOwner = false };
                    }
                }
                catch (Exception ex)
                {
                    fileStream.Seek(0, SeekOrigin.Begin);
                    readStream = fileStream;
                }
            }

            return readStream;
        }
        
        [SuppressMessage("ReSharper", "AccessToModifiedClosure")]
        [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
        internal static void DD_ISO(USB.UsbDisk usb, string filePath, InterLink.InterProgress interProgress)
        {
            using var fileStream = new FileStream(filePath, System.IO.FileMode.Open, FileAccess.Read, FileShare.Read);

            Wrap.Retry().ExecuteSafe(() =>
            {
                Drive.PrepareDrive(usb);
            }, true);

            using SafeFileHandle targetHandle = Wrap.Retry().Execute(() => Drive.GetDriveHandle(usb.Index, Win32.FileAttributes.NoBuffering));

            // Lock any remaining logical handles in case we
            // couldn't delete them all with PrepareDrive
            Drive.LockLogicalHandles(usb, targetHandle, out List<SafeFileHandle> logicalHandles);

            var geometry = Drive.GetGeometry(targetHandle);

            uint chunkSize = (((1024 * 1024) + geometry.Geometry.BytesPerSector - 1) / geometry.Geometry.BytesPerSector) * geometry.Geometry.BytesPerSector;
            var buffer = new byte[chunkSize];

            bool useSecondBuffer = false;
            var secondBuffer = new byte[chunkSize];

            using (var targetStream = new FileStream(targetHandle, FileAccess.ReadWrite))
            {
                Stream readStream = null;
                Task writeTask = Task.CompletedTask;

                long offset = 0;
                long totalRead = 0;
                double progress = 0;

                ResiliencePipeline readPipeline = new ResiliencePipelineBuilder()
                    .AddRetry(new RetryStrategyOptions()
                    {
                        MaxRetryAttempts = 2,
                        Delay = TimeSpan.FromMilliseconds(2000),
                        OnRetry = args =>
                        {
                            Log.WriteExceptionSafe(args.Outcome.Exception);

                            if (!(readStream is FileStream))
                                readStream!.Dispose();

                            if (args.AttemptNumber == 0 && readStream is FileStream)
                            {
                                if (!Wrap.ExecuteSafe(() => fileStream.Seek(totalRead, SeekOrigin.Begin)).Failed)
                                    return default;
                            }

                            fileStream.Seek(0, SeekOrigin.Begin);
                            readStream = GetFileReadStream(fileStream);

                            long retryRead = 0;
                            while (retryRead < totalRead)
                            {
                                retryRead += readStream.Read(useSecondBuffer ? secondBuffer : buffer, 0, useSecondBuffer ? secondBuffer.Length : buffer.Length);
                            }

                            return default;
                        },
                    })
                    .Build();


                long currentChunkSize = chunkSize;
                readStream = GetFileReadStream(fileStream);
                try
                {
                    int amountRead;
                    while ((amountRead = readPipeline.Execute(() => readStream.Read(useSecondBuffer ? secondBuffer : buffer, 0, useSecondBuffer ? secondBuffer.Length : buffer.Length))) > 0)
                    {
                        totalRead += amountRead;

                        Wrap.Retry().Execute(() =>
                        {
                            try
                            {
                                try
                                {
                                    writeTask.Wait();
                                }
                                catch (AggregateException exception)
                                {
                                    Log.EnqueueExceptionSafe(exception);
                                    throw;
                                }
                            }
                            catch (Exception e)
                            {
                                targetStream.Seek(Math.Max(offset - currentChunkSize, 0), SeekOrigin.Begin);
                                writeTask = targetStream.WriteAsync(!useSecondBuffer ? secondBuffer : buffer, 0, !useSecondBuffer ? secondBuffer.Length : buffer.Length);
                                throw;
                            }
                        });

                        currentChunkSize = chunkSize;
                        if (amountRead != buffer.Length)
                        {
                            currentChunkSize = amountRead;
                            if (currentChunkSize % geometry.Geometry.BytesPerSector != 0)
                            {
                                currentChunkSize =
                                    ((currentChunkSize + geometry.Geometry.BytesPerSector - 1) /
                                     geometry.Geometry.BytesPerSector) * geometry.Geometry.BytesPerSector;
                            }

                            if (useSecondBuffer)
                                secondBuffer = new byte[currentChunkSize];
                            else
                                buffer = new byte[currentChunkSize];
                        }

                        Wrap.Retry().Execute(() =>
                        {
                            targetStream.Seek(offset, SeekOrigin.Begin);
                            writeTask = targetStream.WriteAsync(useSecondBuffer ? secondBuffer : buffer, 0, useSecondBuffer ? secondBuffer.Length : buffer.Length);
                        });

                        var currentProgress = Math.Round((fileStream.Position / (double)fileStream.Length) * 100, 1);
                        if (currentProgress > progress)
                        {
                            progress = currentProgress;
                            interProgress.Report((decimal)Math.Min(10 + (progress * 0.88), 98));
                        }

                        offset += currentChunkSize;
                        useSecondBuffer = !useSecondBuffer;
                    }
                }
                finally
                {
                    readStream.Dispose();
                }
            }

            FlushFileBuffers(targetHandle);
            // ReSharper disable once DisposeOnUsingVariable
            targetHandle.Dispose();

            logicalHandles.ForEach(x =>
            {
                FlushFileBuffers(x);
                x.Dispose();
            });
        }

        [DllImport("kernel32.dll")]
        static extern bool SetFilePointerEx(SafeFileHandle hFile, long liDistanceToMove,
            out long lpNewFilePointer, uint dwMoveMethod);

        internal static void DD_EFI_ISO(ref SafeFileHandle handle, uint driveIndex, long efiOffset)
        {
            SetFilePointerEx(handle, efiOffset, out long returnOffset, 0);
            using (UnmanagedMemoryStream stream = (UnmanagedMemoryStream)Assembly.GetExecutingAssembly()
                       .GetManifestResourceStream("KTWirzade.Shared.Properties.uefi-ntfs-ame.img"))
            {
                var buffer = new byte[stream.Length];
                stream.Read(buffer, 0, (int)stream.Length);

                string error = null;
                for (int i = 0; i < 10; i++)
                {
                    if (i != 0)
                    {
                        handle.Dispose();
                        handle = Drive.GetDriveHandle(driveIndex);
                    }

                    bool success = WriteFile(handle, buffer, (uint)stream.Length, out uint bytesWritten, IntPtr.Zero);
                    if (success)
                    {
                        error = null;
                        FlushFileBuffers(handle);
                        break;
                    }
                    error = Marshal.GetLastWin32Error().ToString();

                    Thread.Sleep(500);
                }
                if (error != null)
                    Log.WriteSafe(LogType.Error, "Could not write EFI image: " + error, null);
            }

        }


        internal static async Task WriteISO(string filePath, string path, [CanBeNull] IProgress<decimal> mainProgress)
        {
            var size =
                new FileInfo(filePath).Length;
            using (FileStream fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                VfsFileSystemFacade reader;
                try
                {
                    reader = new UdfReader(fileStream);
                }
                catch (Exception e)
                {
                    reader = new CDReader(fileStream, true);

                    if (reader.GetFileSystemEntries(@"\" + reader.Root.Name).All(x => x.TrimStart('\\').Equals("README.TXT", StringComparison.OrdinalIgnoreCase)))
                    {
                        reader.Dispose();
                        throw new Exception("UDF open failed and CDReader README found.");
                    }
                }

                var progress = new Progress<long>(x =>
                {
                    if (mainProgress != null)
                        mainProgress.Report(Math.Min(10 + (((decimal)(x * 100) / size) * (decimal)0.88), 98));
                });                await Task.Run(() =>
                {
                    ExtractISODirectory(reader.Root, path, "", progress, 0, 0);
                });
                reader.Dispose();
                
                mainProgress?.Report(100);
            }
        }

        private static (long PendingBytes, long TotalBytes) ExtractISODirectory(DiscDirectoryInfo dirInfo, string rootPath, string pathInISO, IProgress<long> progress, long pendingBytes, long totalBytes)
        {
            if (!string.IsNullOrWhiteSpace(pathInISO))
            {
                pathInISO += "\\" + dirInfo.Name;
            }

            rootPath += "\\" + dirInfo.Name;

            if (!Directory.Exists(rootPath))
            {
                Wrap.RetryExponential.Execute(() => Directory.CreateDirectory(rootPath));
            }
            foreach (DiscDirectoryInfo subDirInfo in dirInfo.GetDirectories())
            {
                var byteInfo = ExtractISODirectory(subDirInfo, rootPath, pathInISO, progress, pendingBytes, totalBytes);
                pendingBytes = byteInfo.PendingBytes;
                totalBytes = byteInfo.TotalBytes;
            }

            foreach (DiscFileInfo fileInfo in dirInfo.GetFiles())
            {
                Wrap.RetryExponential.Execute(() =>
                {
                    using (Stream fileStream = fileInfo.OpenRead())
                    {
                        using (FileStream destination = File.Create(rootPath + "\\" + fileInfo.Name))
                        {
                            var buffer = new byte[4096];
                            int bytesRead;
                            var tenMb = 10L * 1024 * 1024;
                            while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                destination.Write(buffer, 0, bytesRead);
                                pendingBytes += bytesRead;
                                if (pendingBytes > tenMb)
                                {
                                    totalBytes += pendingBytes;
                                    pendingBytes = 0;
                                    progress.Report(totalBytes);
                                }
                            }
                        }
                    }
                });
            }
            
            return (pendingBytes, totalBytes);
        }
    }
}