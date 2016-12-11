using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Prometheus.Services.Model;

namespace Prometheus.Services.UnitTests
{
    public class CodeGeneratorTests {
        [TestCaseSource(nameof(CodeGeneratorCases))]
        public void CodeGenerator_IdentifiesRelationalExpressions_Correctly(string codeInput, string[] globalVariables, Operation[] operations) {
            var codeVisitor = new CodeGenerator(null);
            codeVisitor.Visit(codeInput);
        }

        #region Test Case Sources

        public IEnumerable<TestCaseData> CodeGeneratorCases
        {
            get
            {
                #region First case
                yield return new TestCaseData(@"struct node {
                                                   int data;
                                                   struct node *next;
                                                };

                                                struct node *head = NULL;

                                                void insertFirst() {
                                                   struct node *variable;

                                                   if(head->next==variable || (head->next!=head && head->data == variable->data)){
                                                   }
                                                }",
                    new[] { "globalFirst", "globalSecond", "head" },
                    new[]
                    {
                        new Operation("insertFirst", new List<Variable>
                        {
                            new Variable("localFirst", "int", "insertFirst") {DependentVariables = new HashSet<string>(), LinksToGlobalState = false},
                            new Variable("localSecond", "int", "insertFirst") {DependentVariables = new HashSet<string>(), LinksToGlobalState = false},
                            new Variable("localThird", "struct node*", "insertFirst") {DependentVariables = new HashSet<string> {"head"}, LinksToGlobalState = true},
                        }
                            ),
                        new Operation("main")
                    });
                #endregion
            }
        }

        #endregion
    }
}