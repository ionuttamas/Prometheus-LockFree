using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using Prometheus.Common;
using Prometheus.Services.Parser;

namespace Prometheus.Services.Extensions {
    //todo: clean this up and leave only what's necessary
    public static class RuleContextExtensions {
        public static string GetName(this ParserRuleContext context)
        {
            return context.Start.Text;
        }

        public static int GetStartIndex(this ParserRuleContext context) {
            return context.Start.StartIndex;
        }

        public static int GetStopIndex(this ParserRuleContext context) {
            return context.Stop.StopIndex;
        }

        public static string GetContextText(this ParserRuleContext context)
        {
            string text = context.Start.InputStream.GetText(Interval.Of(context.Start.StartIndex, context.Stop.StopIndex));

            return text;
        }

        public static string GetContextText(this ParserRuleContext context, int start, int end)
        {
            string text = context.Start.InputStream.GetText(Interval.Of(start, end));

            return text;
        }

        public static bool ContainsIndex(this ParserRuleContext context, int index)
        {
            return context.GetStartIndex() <= index && index <= context.GetStopIndex();
        }

        public static CLanguageParser.FunctionDefinitionContext GetFunction(this RuleContext context)
        {
            var result = context.GetAncestor<CLanguageParser.FunctionDefinitionContext>();

            return result;
        }

        public static List<T> GetDescendants<T>(this RuleContext context, Func<RuleContext, bool> filter=null)
            where T : RuleContext
        {
            var result = new List<T>();

            if (filter == null) {
                filter = x => x is T;
            }

            if (filter(context)) {
                result.Add((T)context);
            }

            for (int i = 0; i < context.ChildCount; i++) {
                if (context.GetChild(i) is RuleContext) {
                    RuleContext ruleContext = (RuleContext)context.GetChild(i);
                    List<T> decendants = ruleContext.GetDescendants<T>(filter);

                    if (decendants != null)
                        result.AddRange(decendants);
                }
            }

            return result;
        }

        public static List<RuleContext> GetLeafDescendants(this RuleContext context, Func<RuleContext, bool> filter)
        {
            var descendants = Enumerable
                .Range(0, context.ChildCount)
                .Select(context.GetChild)
                .OfType<RuleContext>()
                .SelectMany(x => x.GetLeafDescendants(filter))
                .ToList();

            if (filter(context) && !descendants.Any())
            {
                return new List<RuleContext> {context};
            }

            return descendants;
        }

        public static List<T> GetFirstLevelDescendants<T>(this RuleContext context, Func<RuleContext, bool> filter = null)
            where T : RuleContext
        {
            if (filter == null) {
                filter = x => x is T;
            }

            var result = new List<T>();
            var children = Enumerable
                .Range(0, context.ChildCount)
                .Select(context.GetChild)
                .OfType<RuleContext>()
                .ToList();
            var levelQueue = new Queue<RuleContext>(children);
            var temporaryQueue = new Queue<RuleContext>();
            bool levelFound = false;

            while (!levelFound && levelQueue.Count > 0)
            {
                foreach (var ruleContext in levelQueue) {
                    if (filter(ruleContext))
                    {
                        levelFound = true;
                        result.Add((T) ruleContext);
                    }

                    children = Enumerable
                            .Range(0, ruleContext.ChildCount)
                            .Select(ruleContext.GetChild)
                            .OfType<RuleContext>()
                            .ToList();
                    temporaryQueue.EnqueueRange(children);
                }

                levelQueue.Clear();
                levelQueue.EnqueueRange(temporaryQueue);
                temporaryQueue.Clear();
            }

            //Due to the parse tree representation, expression that should be found on the same level, can be offset by a level
            //Thus, we consider expressions in 2 levels at a time
            result.AddRange(levelQueue.Where(ruleContext => filter(ruleContext)).Cast<T>());
            result = result.Distinct(x => x).ToList();

            return result;
        }

        public static T GetFirstDescendant<T>(this RuleContext context, Func<RuleContext, bool> filter = null)
            where T : RuleContext
        {
            if (filter == null) {
                filter = x => x is T;
            }

            var result = context.GetDescendants<T>(filter)[0];

            return result;
        }

        public static T GetDirectDescendant<T>(this RuleContext context, Func<RuleContext, bool> filter = null)
            where T: RuleContext
        {
            if (context.ChildCount == 0)
                return null;

            IParseTree firstChild = context.GetChild(0);

            if (!(firstChild is RuleContext))
                return null;

            var childContext = (RuleContext)firstChild;

            if (filter == null)
            {
                filter = x => x is T;
            }

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

        public static List<T> GetDirectDescendants<T>(this RuleContext context, Func<RuleContext, bool> filter = null)
            where T: RuleContext
        {
            var result = new List<T>();

            if (filter == null)
            {
                filter = x => x is T;
            }

            for (int i = 0; i < context.ChildCount; i++) {
                if (context.GetChild(i) is RuleContext) {
                    RuleContext ruleContext = (RuleContext)context.GetChild(i);

                    if (filter(ruleContext))
                        result.Add((T)ruleContext);
                }
            }

            return result;
        }

        public static T GetAncestor<T>(this RuleContext context) where T:RuleContext
        {
            RuleContext ancestorMatch = context.Parent;

            while (ancestorMatch != null && !(ancestorMatch is T))
            {
                ancestorMatch = ancestorMatch.Parent;
            }

            return (T)ancestorMatch;
        }
    }
}
