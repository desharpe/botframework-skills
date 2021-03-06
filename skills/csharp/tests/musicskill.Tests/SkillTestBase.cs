﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Adapters;
using Microsoft.Bot.Builder.AI.Luis;
using Microsoft.Bot.Builder.LanguageGeneration;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Solutions;
using Microsoft.Bot.Solutions.Responses;
using Microsoft.Bot.Solutions.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MusicSkill.Bots;
using MusicSkill.Dialogs;
using MusicSkill.Services;
using MusicSkill.Tests.Mocks;
using MusicSkill.Tests.Utilities;

namespace MusicSkill.Tests
{
    public class SkillTestBase : BotTestBase
    {
        public IServiceCollection Services { get; set; }

        public LocaleTemplateManager TemplateEngine { get; set; }

        public IServiceManager ServiceManager { get; set; }

        [TestInitialize]
        public virtual void InitializeSkill()
        {
            ServiceManager = MockServiceManager.GetServiceManager();
            Services = new ServiceCollection();
            Services.AddSingleton(new BotSettings());
            Services.AddSingleton(new BotServices());

            Services.AddSingleton<IBotTelemetryClient, NullBotTelemetryClient>();
            Services.AddSingleton(new MicrosoftAppCredentials("appId", "password"));
            Services.AddSingleton(new UserState(new MemoryStorage()));
            Services.AddSingleton(new ConversationState(new MemoryStorage()));
            Services.AddSingleton(sp =>
            {
                var userState = sp.GetService<UserState>();
                var conversationState = sp.GetService<ConversationState>();
                return new BotStateSet(userState, conversationState);
            });

            var localizedTemplates = new Dictionary<string, string>();
            var templateFile = "ResponsesAndTexts";
            var supportedLocales = new List<string>() { "en-us", "zh-cn" };

            foreach (var locale in supportedLocales)
            {
                // LG template for en-us does not include locale in file extension.
                var localeTemplateFile = locale.Equals("en-us")
                    ? Path.Combine(".", "Responses", "ResponsesAndTexts", $"{templateFile}.lg")
                    : Path.Combine(".", "Responses", "ResponsesAndTexts", $"{templateFile}.{locale}.lg");

                localizedTemplates.Add(locale, localeTemplateFile);
            }

            TemplateEngine = new LocaleTemplateManager(localizedTemplates, "en-us");

            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en-us");
            Services.AddSingleton(TemplateEngine);
            Services.AddSingleton(ServiceManager);
            Services.AddTransient<MainDialog>();
            Services.AddTransient<PlayMusicDialog>();
            Services.AddTransient<ControlSettingsDialog>();
            Services.AddSingleton<TestAdapter, DefaultTestAdapter>();
            Services.AddTransient<IBot, DefaultActivityHandler<MainDialog>>();
        }

        public TestFlow GetTestFlow()
        {
            var sp = Services.BuildServiceProvider();
            var adapter = sp.GetService<TestAdapter>();

            var testFlow = new TestFlow(adapter, async (context, token) =>
            {
                var bot = sp.GetService<IBot>();
                await bot.OnTurnAsync(context, CancellationToken.None);
            });

            return testFlow;
        }

        public TestFlow GetSkillTestFlow()
        {
            var sp = Services.BuildServiceProvider();
            var adapter = sp.GetService<TestAdapter>();

            var testFlow = new TestFlow(adapter, async (context, token) =>
            {
                // Set claims in turn state to simulate skill mode
                var claims = new List<Claim>();
                claims.Add(new Claim(AuthenticationConstants.VersionClaim, "1.0"));
                claims.Add(new Claim(AuthenticationConstants.AudienceClaim, Guid.NewGuid().ToString()));
                claims.Add(new Claim(AuthenticationConstants.AppIdClaim, Guid.NewGuid().ToString()));
                context.TurnState.Add("BotIdentity", new ClaimsIdentity(claims));

                var bot = sp.GetService<IBot>();
                await bot.OnTurnAsync(context, CancellationToken.None);
            });

            return testFlow;
        }

        public string[] ParseReplies(string templateName, object data = null)
        {
            var path = CultureInfo.CurrentCulture.Name.ToLower() == "en-us" ?
                Path.Combine(".", "Responses", "ResponsesAndTexts", "ResponsesAndTexts.lg") :
                Path.Combine(".", "Responses", "ResponsesAndTexts", $"ResponsesAndTexts.{CultureInfo.CurrentUICulture.Name.ToLower()}.lg");

            return Templates.ParseFile(path).ExpandTemplate(templateName + ".Text", data).Select(obj => obj.ToString()).ToArray();
        }
    }
}
