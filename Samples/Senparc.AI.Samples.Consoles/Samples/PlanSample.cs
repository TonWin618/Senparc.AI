﻿using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.Skills.Core;
using Senparc.AI.Interfaces;
using Senparc.AI.Kernel;
using Senparc.AI.Kernel.Handlers;

namespace Senparc.AI.Samples.Consoles.Samples
{
    public class PlanSample
    {
        IAiHandler _aiHandler;

        SemanticAiHandler _semanticAiHandler => (SemanticAiHandler)_aiHandler;
        string _userId = "Jeffrey";

        public PlanSample(IAiHandler aiHandler)
        {
            _aiHandler = aiHandler;
        }

        public async Task RunAsync()
        {

            await Console.Out.WriteLineAsync("PlanSample 开始运行。请输入需要生成的内容：");


            await Console.Out.WriteLineAsync("请输入");

            var iWantToRun = _semanticAiHandler
                           .IWantTo()
                           .ConfigModel(ConfigModel.TextCompletion, _userId, SampleHelper.Default_TextCompletion)
                           .BuildKernel();

            //var planner = iWantToRun.ImportSkill(new TextMemorySkill(iWantToRun.Kernel.Memory)).skillList;

            var dir = Path.GetDirectoryName(this.GetType().Assembly.Location);//System.IO.Directory.GetCurrentDirectory();
            //Console.WriteLine("dir:" + dir);

            var skillsDirectory = Path.Combine(dir, "..", "..", "..", "skills");
            //Console.WriteLine("skillsDirectory:" + skillsDirectory);

            await Console.Out.WriteLineAsync("Add Your Skills, input q to finish");
            var skill = Console.ReadLine();
            while (skill != "q")
            {
                //SummarizeSkill , WriterSkill , ...
                iWantToRun.ImportSkillFromDirectory(skillsDirectory, skill);
                skill = Console.ReadLine();
            }

            await Console.Out.WriteLineAsync("Tell me your task:");
            //Tomorrow is Valentine's day. I need to come up with a few date ideas and e-mail them to my significant other
            var ask = Console.ReadLine();
            await Console.Out.WriteLineAsync();


            var planner = new SequentialPlanner(iWantToRun.Kernel);
            //var ask = "If my investment of 2130.23 dollars increased by 23%, how much would I have after I spent 5 on a latte?";

            var requestSettings = new Microsoft.SemanticKernel.AI.TextCompletion.CompleteRequestSettings()
            {
                Temperature = 0.01,
                MaxTokens = 5000,
                TopP = 0.1,
            };

            var plan = await planner.CreatePlanAsync(ask);

            await Console.Out.WriteLineAsync("Plan:");
            await Console.Out.WriteLineAsync(plan.ToJson(true));

            // Execute the plan
            var result = await plan.InvokeAsync(settings: requestSettings);

            Console.WriteLine("Plan results:");
            Console.WriteLine(result.Result);
            Console.WriteLine();

            await Console.Out.WriteLineAsync("Now system will add a new plan into your request: Rewrite the above in the style of Shakespeare. Press Enter");

            Console.ReadLine();

            //新建计划，并执行 Plan：

            string prompt = @"
{{$input}}

Rewrite the above in the style of Shakespeare.
Give me the plan less than 5 steps.
";
            var shakespeareFunction = iWantToRun.CreateSemanticFunction(prompt, "shakespeare", "ShakespeareSkill", maxTokens: 2000, temperature: 0.2, topP: 0.5).function;

            var newPlan = await planner.CreatePlanAsync(ask);
            await Console.Out.WriteLineAsync("New Plan:");
            await Console.Out.WriteLineAsync(newPlan.ToJson(true));

            Console.WriteLine("Updated plan:\n");
            // Execute the plan
            var newResult = await newPlan.InvokeAsync(settings: requestSettings);

            Console.WriteLine("Plan results:");
            Console.WriteLine(newResult.Result);
            Console.WriteLine();

            await Console.Out.WriteLineAsync("== plan execute finish ==");

            await Console.Out.WriteLineAsync();

        }

    }


}