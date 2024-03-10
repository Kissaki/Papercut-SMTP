﻿// Papercut
// 
// Copyright © 2008 - 2012 Ken Robertson
// Copyright © 2013 - 2024 Jaben Cargman
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.


using Papercut.Core.Domain.Paths;

namespace Papercut.Message;

public class MessageRepository
{
    public const string MessageFileSearchPattern = "*.eml";

    readonly ILogger _logger;

    readonly IMessagePathConfigurator _messagePathConfigurator;

    public MessageRepository(ILogger logger, IMessagePathConfigurator messagePathConfigurator)
    {
        this._logger = logger;
        this._messagePathConfigurator = messagePathConfigurator;
    }

    public bool DeleteMessage(MessageEntry entry)
    {
        // Delete the file and remove the entry
        if (!File.Exists(entry.File))
            return false;

        var attributes = File.GetAttributes(entry.File);

        try
        {
            if (attributes.HasFlag(FileAttributes.ReadOnly))
            {
                // remove read only attribute
                File.SetAttributes(entry.File, attributes ^ FileAttributes.ReadOnly);
            }

        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException(
                $"Unable to remove read-only attribute on file '{entry.File}'",
                ex);
        }

        File.Delete(entry.File);
        return true;
    }

    public byte[]? GetMessage(string? file)
    {
        if (!File.Exists(file))
            throw new IOException($"File {file} Does Not Exist");

        var info = new FileInfo(file);
        byte[]? data;
        int retryCount = 0;

        while (!info.TryReadFile(out data))
        {
            Thread.Sleep(500);

            if (++retryCount > 10)
            {
                throw new IOException(
                    $"Cannot Load File {file} After 5 Seconds");
            }
        }

        return data;
    }

    public IList<MessageEntry> LoadMessages()
    {
        IEnumerable<string?> files = this._messagePathConfigurator.LoadPaths.SelectMany(
            p => Directory.GetFiles(p, MessageFileSearchPattern));

        return
            files.Select(file => new MessageEntry(file))
                .OrderByDescending(m => m.ModifiedDate)
                .ThenBy(m => m.Name)
                .ToList();
    }

    public async Task<string?> SaveMessageAsync(Func<FileStream, Task> writeTo)
    {
        string? fileName = null;

        try
        {
            // the file must not exists.  the resolution of DataTime.Now may be slow w.r.t. the speed of the received files
            fileName = Path.Combine(
                this._messagePathConfigurator.DefaultSavePath,
                $"{DateTime.Now:yyyyMMddHHmmssfff}-{StringHelpers.SmallRandomString()}.eml");

            await using (var fileStream = File.Create(fileName))
            {
                await writeTo(fileStream);
            }

            this._logger.Information("Successfully Saved email message: {EmailMessageFile}", fileName);
        }
        catch (Exception ex)
        {
            this._logger.Error(ex, "Failure saving email message: {EmailMessageFile}", fileName);
        }

        return fileName;
    }
}