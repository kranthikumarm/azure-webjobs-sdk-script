// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Dependencies;
using Microsoft.Azure.AppService.Proxy.Client.Contract;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Script.Description;
using Microsoft.Azure.WebJobs.Script.WebHost;
using Microsoft.Azure.WebJobs.Script.WebHost.Controllers;
using Microsoft.Azure.WebJobs.Script.WebHost.WebHooks;

namespace Microsoft.Azure.WebJobs.Script.Host
{
    public class ProxyFunctionExecutor : IFuncExecutor
    {
        private readonly WebScriptHostManager _scriptHostManager;
        private readonly IDependencyResolver _dependencyResolver;
        private WebHookReceiverManager _webHookReceiverManager;

        internal ProxyFunctionExecutor(WebScriptHostManager scriptHostManager, WebHookReceiverManager webHookReceiverManager, IDependencyResolver dependencyResolver)
        {
            _scriptHostManager = scriptHostManager;
            _webHookReceiverManager = webHookReceiverManager;
            _dependencyResolver = dependencyResolver;
        }

        public async Task ExecuteFuncAsync(string funcName, Dictionary<string, object> arguments, CancellationToken cancellationToken)
        {
            HttpRequestMessage request = arguments["MS_AzureFunctionsHttpRequest"] as HttpRequestMessage;
            var function = _scriptHostManager.GetHttpFunctionOrNull(request);
            if (function == null)
            {
                // request does not map to an HTTP function
                request.Properties["MS_AzureFunctionsHttpResponse"] = new HttpResponseMessage(HttpStatusCode.NotFound);
                return;
            }
            request.SetProperty(ScriptConstants.AzureFunctionsHttpFunctionKey, function);

            var authorizationLevel = await FunctionsController.DetermineAuthorizationLevelAsync(request, function, _dependencyResolver);
            if (function.Metadata.IsExcluded ||
               (function.Metadata.IsDisabled && !(request.IsAuthDisabled() || authorizationLevel == AuthorizationLevel.Admin)))
            {
                // disabled functions are not publicly addressable w/o Admin level auth,
                // and excluded functions are also ignored here (though the check above will
                // already exclude them)
                request.Properties["MS_AzureFunctionsHttpResponse"] = new HttpResponseMessage(HttpStatusCode.NotFound);
                return;
            }

            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> processRequestHandler = async (req, ct) =>
            {
                return await ProcessRequestAsync(req, function, ct);
            };

            var resp = await _scriptHostManager.HttpRequestManager.ProcessRequestAsync(request, processRequestHandler, cancellationToken);
            request.Properties["MS_AzureFunctionsHttpResponse"] = resp;
            return;
        }

        private async Task<HttpResponseMessage> ProcessRequestAsync(HttpRequestMessage request, FunctionDescriptor function, CancellationToken cancellationToken)
        {
            var httpTrigger = function.GetTriggerAttributeOrNull<HttpTriggerAttribute>();
            bool isWebHook = !string.IsNullOrEmpty(httpTrigger.WebHookType);
            var authorizationLevel = request.GetAuthorizationLevel();
            HttpResponseMessage response = null;

            if (isWebHook)
            {
                if (request.HasAuthorizationLevel(AuthorizationLevel.Admin))
                {
                    // Admin level requests bypass the WebHook auth pipeline
                    response = await _scriptHostManager.HandleRequestAsync(function, request, cancellationToken);
                }
                else
                {
                    // This is a WebHook request so define a delegate for the user function.
                    // The WebHook Receiver pipeline will first validate the request fully
                    // then invoke this callback.
                    Func<HttpRequestMessage, Task<HttpResponseMessage>> invokeFunction = async (req) =>
                    {
                        // Reset the content stream before passing the request down to the function
                        Stream stream = await req.Content.ReadAsStreamAsync();
                        stream.Seek(0, SeekOrigin.Begin);

                        return await _scriptHostManager.HandleRequestAsync(function, req, cancellationToken);
                    };
                    response = await _webHookReceiverManager.HandleRequestAsync(function, request, invokeFunction);
                }
            }
            else
            {
                // Authorize
                if (!request.HasAuthorizationLevel(httpTrigger.AuthLevel))
                {
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized);
                }

                // Not a WebHook request so dispatch directly
                response = await _scriptHostManager.HandleRequestAsync(function, request, cancellationToken);
            }

            return response;
        }
    }
}
