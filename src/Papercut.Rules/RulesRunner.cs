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


using Autofac;

namespace Papercut.Rules;

public class RulesRunner : IRulesRunner
{
    readonly MethodInfo _dispatchRuleMethod;

    readonly ILifetimeScope _lifetimeScope;

    readonly ILogger _logger;

    public RulesRunner(ILifetimeScope lifetimeScope, ILogger logger)
    {
        this._lifetimeScope = lifetimeScope;
        this._logger = logger;
        this._dispatchRuleMethod = this.GetType()
            .GetMethod("DispatchRule", BindingFlags.NonPublic | BindingFlags.Instance);
    }

    public void Run([NotNull] IRule[] rules, [NotNull] MessageEntry messageEntry)
    {
        if (rules == null) throw new ArgumentNullException(nameof(rules));
        if (messageEntry == null) throw new ArgumentNullException(nameof(messageEntry));

        foreach (IRule rule in rules.Where(_ => _.IsEnabled))
        {
            this._dispatchRuleMethod.MakeGenericMethod(rule.GetType())
                .Invoke(this, [rule, messageEntry]);
        }
    }

    void DispatchRule<TRule>(TRule rule, MessageEntry messageEntry)
        where TRule : IRule
    {
        this._logger.Information(
            "Running Rule Dispatch for Rule {Rule} on Message {@MessageEntry}",
            rule,
            messageEntry);

        try
        {
            var ruleDispatcher = this._lifetimeScope.Resolve<IRuleDispatcher<TRule>>();
            ruleDispatcher.Dispatch(rule, messageEntry);
        }
        catch (Exception ex)
        {
            this._logger.Warning(
                ex,
                "Failure Dispatching Rule {Rule} for Message {@MessageEntry}",
                rule,
                messageEntry);
        }
    }
}