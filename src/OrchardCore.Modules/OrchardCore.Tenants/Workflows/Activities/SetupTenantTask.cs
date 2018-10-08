using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Models;
using OrchardCore.Modules;
using OrchardCore.Setup.Services;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace OrchardCore.Tenants.Workflows.Activities
{
    public class SetupTenantTask : TenantTask
    {
        private readonly IClock _clock;
        private readonly IWorkflowExpressionEvaluator _expressionEvaluator;

        public SetupTenantTask(IShellSettingsManager shellSettingsManager, IShellHost shellHost, ISetupService setupService, IClock clock, IWorkflowExpressionEvaluator expressionEvaluator, IWorkflowScriptEvaluator scriptEvaluator, IStringLocalizer<SetupTenantTask> localizer) 
            : base(shellSettingsManager, shellHost, scriptEvaluator, localizer)
        {
            SetupService = setupService;
            _clock = clock;
            _expressionEvaluator = expressionEvaluator;
        }

        protected ISetupService SetupService { get; }

        public override string Name => nameof(SetupTenantTask);
        public override LocalizedString Category => T["Tenant"];
        
        public WorkflowExpression<string> SiteName
        {
            get => GetProperty(() => new WorkflowExpression<string>());
            set => SetProperty(value);
        }

        public WorkflowExpression<string> AdminUsername
        {
            get => GetProperty(() => new WorkflowExpression<string>());
            set => SetProperty(value);
        }

        public WorkflowExpression<string> AdminEmail
        {
            get => GetProperty(() => new WorkflowExpression<string>());
            set => SetProperty(value);
        }

        public WorkflowExpression<string> AdminPassword
        {
            get => GetProperty(() => new WorkflowExpression<string>());
            set => SetProperty(value);
        }

        public WorkflowExpression<string> DatabaseProvider
        {
            get => GetProperty(() => new WorkflowExpression<string>());
            set => SetProperty(value);
        }

        public WorkflowExpression<string> DatabaseConnectionString
        {
            get => GetProperty(() => new WorkflowExpression<string>());
            set => SetProperty(value);
        }

        public WorkflowExpression<string> DatabaseTablePrefix
        {
            get => GetProperty(() => new WorkflowExpression<string>());
            set => SetProperty(value);
        }

        public WorkflowExpression<string> RecipeName
        {
            get => GetProperty(() => new WorkflowExpression<string>());
            set => SetProperty(value);
        }
        
        public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        {
            return Outcomes(T["Running"]);
        }

        public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        {
            var tenantNameTask = _expressionEvaluator.EvaluateAsync(TenantName, workflowContext);
            var siteNameTask = _expressionEvaluator.EvaluateAsync(SiteName, workflowContext);
            var adminUsernameTask = _expressionEvaluator.EvaluateAsync(AdminUsername, workflowContext);
            var adminEmailTask = _expressionEvaluator.EvaluateAsync(AdminEmail, workflowContext);
            var adminPasswordTask = _expressionEvaluator.EvaluateAsync(AdminPassword, workflowContext);
            var databaseProviderTask = _expressionEvaluator.EvaluateAsync(DatabaseProvider, workflowContext);
            var databaseConnectionStringTask = _expressionEvaluator.EvaluateAsync(DatabaseConnectionString, workflowContext);
            var databaseTablePrefixTask = _expressionEvaluator.EvaluateAsync(DatabaseTablePrefix, workflowContext);
            var recipeNameTask = _expressionEvaluator.EvaluateAsync(RecipeName, workflowContext);

            await Task.WhenAll(tenantNameTask, siteNameTask, adminUsernameTask, adminEmailTask, adminPasswordTask, databaseProviderTask, databaseConnectionStringTask, databaseTablePrefixTask, recipeNameTask);

            if (!ShellSettingsManager.TryGetSettings(tenantNameTask.Result?.Trim(), out var shellSettings))
            {
                if (!string.IsNullOrWhiteSpace(tenantNameTask.Result))
                {
                    shellSettings = new ShellSettings
                    {
                        Name = tenantNameTask.Result?.Trim(),
                        RequestUrlPrefix = tenantNameTask.Result?.Trim(),
                        //RequestUrlPrefix = requestUrlPrefixTask.Result?.Trim(),
                        //RequestUrlHost = requestUrlHostTask.Result?.Trim(),
                        ConnectionString = databaseConnectionStringTask.Result?.Trim(),
                        TablePrefix = databaseTablePrefixTask.Result?.Trim(),
                        DatabaseProvider = databaseProviderTask.Result?.Trim(),
                        State = TenantState.Uninitialized,
                        Secret = Guid.NewGuid().ToString(),
                        RecipeName = recipeNameTask.Result.Trim()
                    };
                }

                ShellSettingsManager.SaveSettings(shellSettings);
                var shellContext = await ShellHost.GetOrCreateShellContextAsync(shellSettings);
            }

            var recipes = await SetupService.GetSetupRecipesAsync();
            var recipe = recipes.FirstOrDefault(x => x.Name == shellSettings.RecipeName);

            var setupContext = new SetupContext
            {
                ShellSettings = shellSettings,
                SiteName = siteNameTask.Result?.Trim(),
                EnabledFeatures = null,
                AdminUsername = adminUsernameTask.Result?.Trim(),
                AdminEmail = adminEmailTask.Result?.Trim(),
                AdminPassword = adminPasswordTask.Result?.Trim(),
                Errors = new Dictionary<string, string>(),
                Recipe = recipe,
                SiteTimeZone = _clock.GetSystemTimeZone().TimeZoneId,
                DatabaseProvider = databaseProviderTask.Result?.Trim(),
                DatabaseConnectionString = databaseConnectionStringTask.Result?.Trim(),
                DatabaseTablePrefix = databaseTablePrefixTask.Result?.Trim(),
            };

            var executionId = await SetupService.SetupAsync(setupContext);

            //// Check if a component in the Setup failed
            //if (setupContext.Errors.Any())
            //{
            //    foreach (var error in setupContext.Errors)
            //    {
            //        ModelState.AddModelError(error.Key, error.Value);
            //    }
            //}

            return Outcomes("Setup");
        }
    }
}