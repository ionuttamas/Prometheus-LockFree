﻿using System;
using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Prometheus.Services.Parser;

namespace Prometheus.Services.Extensions {
    public static class RuleContextExtensions {
        public static string GetName(this ParserRuleContext context)
        {
            return context.Start.Text;
        }

        public static string GetName(this RuleContext context) {
            return ((ParserRuleContext)context).GetName();
        }

        public static CLanguageParser.FunctionDefinitionContext GetFunction(this RuleContext context)
        {
            var result = (CLanguageParser.FunctionDefinitionContext)context.GetAncestor(x => x is CLanguageParser.FunctionDefinitionContext);

            return result;
        }

        public static List<RuleContext> GetDescendants(this RuleContext context, Func<RuleContext, bool> filter) {
            if (filter(context)) {
                return new List<RuleContext> { context };
            }

            var result = new List<RuleContext>();

            for (int i = 0; i < context.ChildCount; i++) {
                if (context.GetChild(i) is RuleContext) {
                    RuleContext ruleContext = (RuleContext)context.GetChild(i);
                    List<RuleContext> decendants = ruleContext.GetDescendants(filter);

                    if (decendants != null)
                        result.AddRange(decendants);
                }
            }

            return result;
        }

        public static T GetFirstDescendant<T>(this RuleContext context, Func<RuleContext, bool> filter)
            where T : RuleContext
        {
            var result = context.GetDescendants(filter)[0];

            return (T)result;
        }

        public static T GetDirectDescendant<T>(this RuleContext context, Func<RuleContext, bool> filter)
            where T: RuleContext
        {
            if (context.ChildCount == 0)
                return null;

            IParseTree firstChild = context.GetChild(0);

            if (!(firstChild is RuleContext))
                return null;

            var childContext = (RuleContext)firstChild;

            while (childContext!=null && !filter(childContext))
            {
                if (childContext.ChildCount == 0)
                    return null;

                firstChild = childContext.GetChild(0);

                if (!(firstChild is RuleContext))
                    return null;

                childContext = (RuleContext)firstChild;
            }

            return (T)childContext;
        }

        public static RuleContext GetAncestor(this RuleContext context, Func<RuleContext, bool> filter)
        {
            RuleContext ancestorMatch = context.Parent;

            while (ancestorMatch != null && !filter(ancestorMatch))
            {
                ancestorMatch = ancestorMatch.Parent;
            }

            return ancestorMatch;
        }
    }
}
