﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Script.Description;

namespace Microsoft.Azure.WebJobs.Script.Dispatch
{
    internal class LanguageWorkerConfig
    {
        public string ExecutablePath { get; set; }

        public string Options { get; set; }

        public string WorkerPath { get; set; }

        public string Arguments { get; set; }

        public ScriptType ScriptType { get; set; } = ScriptType.Unknown;

        public string Extension { get; set; }

        internal int Port { get; set; }

        internal string ToArgumentString(string workerId, string requestId) => $"{Options} {WorkerPath} {Arguments} --host 127.0.0.1 --port {Port} --workerId {workerId} --requestId {requestId}";
    }
}
