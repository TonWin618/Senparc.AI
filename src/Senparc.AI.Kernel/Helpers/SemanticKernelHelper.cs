using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.TextCompletion;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Orchestration;
using Polly;
using Senparc.AI.Entities;
using Senparc.AI.Exceptions;
using Senparc.AI.Interfaces;
using Senparc.AI.Kernel.Entities;
using Senparc.AI.Kernel.Handlers;
using Senparc.CO2NET;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Senparc.AI.Kernel.Helpers
{
    /// <summary>
    /// SemanticKernel 帮助类
    /// </summary>
    public class SemanticKernelHelper
    {
        private IKernel _kernel { get; set; }

        //internal KernelBuilder KernelBuilder { get; set; }
        internal ISenparcAiSetting AiSetting { get; }

        private List<Task> _memoryExecuteList = new List<Task>();


        public SemanticKernelHelper(ISenparcAiSetting? aiSetting = null) =>
            AiSetting = aiSetting ?? Senparc.AI.Config.SenparcAiSetting;

        /// <summary>
        /// 获取对话 ServiceId
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="modelName"></param>
        /// <returns></returns>
        public string GetServiceId(string userId, string modelName)
        {
            return $"{userId}-{modelName}";
        }

        /// <summary>
        /// 获取 SemanticKernel 对象
        /// </summary>
        /// <param name="kernelBuilderAction"><see cref="KernelBuilder"/> 在进行 <see cref="KernelBuilder.Build()"/> 之前需要插入的操作</param>
        /// <param name="refresh" default="false">是否需要刷新kernel</param>
        /// <returns></returns>
        public IKernel GetKernel(Action<KernelBuilder>? kernelBuilderAction = null, bool refresh = false)
        {
            if (_kernel != null && !refresh)
            {
                return _kernel;
            }

            //Kernel = KernelBuilder.Create();

            // var kernelBuilder = Microsoft.SemanticKernel.Kernel.Builder;
            // 以上方法已经被SK标注为 Obsolete, 修改为SK推荐的方法
            var kernelBuilder = new KernelBuilder();

            if(kernelBuilderAction != null) {
                kernelBuilderAction.Invoke(kernelBuilder);
            }

            _kernel = kernelBuilder.Build();

            return _kernel;
        }

        /// <summary>
        /// 获取 Kernel.Memory 对象
        /// </summary>
        /// <returns></returns>
        [Obsolete("该方法已被SK放弃，原文为：Memory functionality will be placed in separate Microsoft.SemanticKernel.Plugins.Memory package. This will be removed in a future release. See sample dotnet/samples/KernelSyntaxExamples/Example14_SemanticMemory.cs in the semantic-kernel repository.")]
        public ISemanticTextMemory GetMemory()
        {
            var kernel = GetKernel();
            if (kernel.Memory == null)
            {
                kernel.UseMemory(new Microsoft.SemanticKernel.Plugins.Memory.VolatileMemoryStore());
            }

            return kernel.Memory;
        }

        /// <summary>
        /// Build 新的 Kernel 对象
        /// </summary>
        /// <param name="kernelBuilder"></param>
        /// <param name="kernelBuilderAction"></param>
        /// <returns></returns>
        public IKernel BuildKernel(KernelBuilder kernelBuilder, Action<KernelBuilder>? kernelBuilderAction = null)
        {
            if(kernelBuilderAction != null ){
                kernelBuilderAction.Invoke(kernelBuilder);
            }

            _kernel = kernelBuilder.Build();
            return _kernel;
        }

        /// <summary>
        /// 设置 Kernel，并配置 TextCompletion 模型
        /// </summary>
        /// <param name="userId">用户ID， 用于防止api滥用</param>
        /// <param name="modelName">模型名 modelId</param>
        /// <param name="senparcAiSetting"></param>
        /// <param name="kernelBuilder"></param>
        /// <returns></returns>
        /// <exception cref="Senparc.AI.Exceptions.SenparcAiException"></exception>
        public KernelBuilder ConfigTextCompletion(string userId, string modelName, ISenparcAiSetting senparcAiSetting,
            KernelBuilder? kernelBuilder)
        {
            var serviceId = GetServiceId(userId, modelName);
            senparcAiSetting ??= Senparc.AI.Config.SenparcAiSetting;
            var aiPlatForm = senparcAiSetting.AiPlatform;

            //TODO 需要判断 Kernel.TextCompletionServices.ContainsKey(serviceId)，如果存在则不能再添加

            // var kernelBuilder = Microsoft.SemanticKernel.Kernel.Builder;
            // 以上方法已经被SK标注为 Obsolete, 修改为SK推荐的方法
            kernelBuilder ??= new KernelBuilder();

            // use `senparcAiSetting` instead of using `AiSetting` from the config file by default
            _ = aiPlatForm switch
            {
                AiPlatform.OpenAI => kernelBuilder.WithOpenAITextCompletionService(modelName, apiKey: senparcAiSetting.ApiKey,
                    orgId: senparcAiSetting.OrganizationId),
                AiPlatform.AzureOpenAI =>
                    // kernelBuilder.WithAzureTextCompletionService(modelName, AiSetting.AzureEndpoint, AiSetting.ApiKey, AiSetting.AzureOpenAIApiVersion),
                    kernelBuilder.WithAzureTextCompletionService(modelName,
                        endpoint: senparcAiSetting.AzureEndpoint,
                        apiKey: senparcAiSetting.ApiKey),
                AiPlatform.NeuCharOpenAI => kernelBuilder.WithAzureTextCompletionService(modelName,
                    endpoint: senparcAiSetting.NeuCharEndpoint,
                    apiKey: senparcAiSetting.ApiKey),
                AiPlatform.HuggingFace => kernelBuilder.WithHuggingFaceTextCompletionService(modelName,
                    endpoint: senparcAiSetting.HuggingFaceEndpoint),
                _ => throw new SenparcAiException($"没有处理当前 {nameof(AiPlatform)} 类型：{aiPlatForm}")
            };

            return kernelBuilder;
        }

        /// <summary>
        /// 设置 Kernel，并配置 TextCompletion 模型
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="modelName"></param>
        /// <param name="kernelBuilder"></param>
        /// <returns></returns>
        /// <exception cref="Senparc.AI.Exceptions.SenparcAiException"></exception>
        public KernelBuilder ConfigTextEmbeddingGeneration(string userId, string modelName, KernelBuilder? kernelBuilder = null)
        {
            //kernel ??= GetKernel();

            var serviceId = GetServiceId(userId, modelName);
            var senparcAiSetting = Senparc.AI.Config.SenparcAiSetting;
            var aiPlatForm = AiSetting.AiPlatform;

            //TODO 需要判断 Kernel.TextCompletionServices.ContainsKey(serviceId)，如果存在则不能再添加

            //TODO：Builder 不应该新建
            
            // var kernelBuilder = Microsoft.SemanticKernel.Kernel.Builder;
            // 以上方法已经被SK标注为 Obsolete, 修改为SK推荐的方法
            kernelBuilder ??= new KernelBuilder();
            
            // use `senparcAiSetting` instead of using `AiSetting` from the config file by default
            _ = aiPlatForm switch
            {
                AiPlatform.OpenAI => kernelBuilder.WithOpenAITextEmbeddingGenerationService(modelName, senparcAiSetting.ApiKey,
                    AiSetting.OrganizationId),

                AiPlatform.AzureOpenAI => kernelBuilder.WithAzureOpenAITextEmbeddingGenerationService(modelName,
                    senparcAiSetting.AzureEndpoint, senparcAiSetting.ApiKey, senparcAiSetting.AzureOpenAIApiVersion),

                AiPlatform.NeuCharOpenAI => kernelBuilder.WithAzureOpenAITextEmbeddingGenerationService(modelName,
                    senparcAiSetting.NeuCharEndpoint, senparcAiSetting.ApiKey, senparcAiSetting.AzureOpenAIApiVersion),

                AiPlatform.HuggingFace => kernelBuilder.WithHuggingFaceTextEmbeddingGenerationService(modelName,
                    senparcAiSetting.HuggingFaceEndpoint),
                
                _ => throw new SenparcAiException($"没有处理当前 {nameof(AiPlatform)} 类型：{aiPlatForm}")
            };

            //kernelBuilder.Configure(c =>
            //{
            //    c.AddTextEmbeddingGenerationService(serviceId, k =>
            //        aiPlatForm switch
            //        {
            //            AiPlatform.OpenAI => new OpenAITextEmbeddingGeneration(modelName, AiSetting.ApiKey, AiSetting.OrganizationId),

            //            AiPlatform.NeuCharOpenAI => new AzureTextEmbeddingGeneration(modelName, AiSetting.NeuCharEndpoint, AiSetting.ApiKey, AiSetting.NeuCharOpenAIApiVersion),

            //            AiPlatform.AzureOpenAI => new AzureTextEmbeddingGeneration(modelName, AiSetting.AzureEndpoint, AiSetting.ApiKey, AiSetting.AzureOpenAIApiVersion),

            //            _ => throw new SenparcAiException($"没有处理当前 {nameof(AiPlatform)} 类型：{aiPlatForm}")
            //        });
            //});

            //TODO:测试多次添加
            //KernelBuilder = builder;

            return kernelBuilder;
        }

        /// <summary>
        /// 设置 DallE 接口，默认强制使用 OpenAI 权限
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="kernelBuilder"></param>
        /// <returns></returns>
        /// <exception cref="SenparcAiException"></exception>
        public KernelBuilder ConfigImageGeneration(string userId, KernelBuilder? kernelBuilder = null)
        {
            var serviceId = GetServiceId(userId, "image-generation");
            var senparcAiSetting = Senparc.AI.Config.SenparcAiSetting;
            var aiPlatForm = AiSetting.AiPlatform;

            //TODO：Builder 不应该新建
            kernelBuilder ??= new KernelBuilder();

            _ = aiPlatForm switch
            {
                AiPlatform.OpenAI => kernelBuilder.WithOpenAIImageGenerationService(AiSetting.ApiKey,
                    AiSetting.OrganizationId),

                AiPlatform.AzureOpenAI => kernelBuilder.WithAzureOpenAIImageGenerationService(AiSetting.AzureEndpoint,
                    AiSetting.ApiKey),

                AiPlatform.NeuCharOpenAI => kernelBuilder.WithAzureOpenAIImageGenerationService(
                    AiSetting.NeuCharEndpoint, AiSetting.ApiKey),

                _ => throw new SenparcAiException($"没有处理当前 {nameof(AiPlatform)} 类型：{aiPlatForm}")
            };

            //kernelBuilder.Configure(c =>
            //{
            //    //c.AddImageGenerationService(serviceId, k =>
            //    //    aiPlatForm switch
            //    //    {
            //    //        AiPlatform.OpenAI => new OpenAIImageGeneration(AiSetting.ApiKey, AiSetting.OrganizationId),

            //    //        AiPlatform.AzureOpenAI => new OpenAIImageGeneration(AiSetting.ApiKey, AiSetting.OrganizationId),

            //    //        _ => throw new SenparcAiException($"没有处理当前 {nameof(AiPlatform)} 类型：{aiPlatForm}")
            //    //    });

            //    //强制使用 OpenAI 权限
            //    c.AddImageGenerationService(serviceId, k =>
            //        new OpenAIImageGeneration(AiSetting.OpenAIKeys.ApiKey, AiSetting.OpenAIKeys.OrganizationId));
            //});

            return kernelBuilder;
        }

        #region Memory 相关

        /// <summary>
        /// Save some information into the semantic memory, keeping only a reference to the source information.
        /// </summary>
        /// <param name="memory">ISemanticTextMemory 对象</param>
        /// <param name="collection">Collection where to save the information</param>
        /// <param name="text">Information to save</param>
        /// <param name="externalId">Unique identifier, e.g. URL or GUID to the original source</param>
        /// <param name="externalSourceName">Name of the external service, e.g. "MSTeams", "GitHub", "WebSite", "Outlook IMAP", etc.</param>
        /// <param name="description">Optional description</param>
        /// <param name="additionalMetadata"></param>
        /// <param name="cancel">Cancellation token</param>
        /// <returns></returns>
        public async Task MemorySaveReferenceAsync(ISemanticTextMemory memory,
            string collection,
            string text,
            string externalId,
            string externalSourceName,
            string? description = null,
            string? additionalMetadata = null,
            CancellationToken cancel = default)
        {
            await memory.SaveReferenceAsync(collection, text, externalId, externalSourceName, description,
                additionalMetadata, cancel);
        }

        /// <summary>
        /// Save some information into the semantic memory, keeping a copy of the source information.
        /// </summary>
        /// <param name="memory">ISemanticTextMemory 对象</param>
        /// <param name="collection">Collection where to save the information</param>
        /// <param name="id">Unique identifier</param>
        /// <param name="text">Information to save</param>
        /// <param name="description">Optional description</param>
        /// <param name="additionalMetadata"></param>
        /// <param name="cancel">Cancellation token</param>        /// <returns></returns>
        public async Task MemorySaveInformationAsync(ISemanticTextMemory memory,
            string collection,
            string text,
            string id,
            string? description = null,
            string? additionalMetadata = null,
            CancellationToken cancel = default)
        {
            await memory.SaveInformationAsync(collection, text, id, description, additionalMetadata, cancel);
        }

        /// <summary>
        /// 添加 Memory 操作
        /// </summary>
        /// <param name="task"></param>
        public void AddMemory(Task task)
        {
            _memoryExecuteList.Add(task);
        }

        /// <summary>
        /// 执行 Memory 操作，并等待
        /// </summary>
        public void ExecuteMemory()
        {
            foreach (var task in _memoryExecuteList)
            {
                Task.Run(() => task);
            }

            Task.WaitAll(_memoryExecuteList.ToArray());
            _memoryExecuteList.Clear();
        }

        #endregion
    }
}