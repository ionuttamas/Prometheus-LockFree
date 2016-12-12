using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Prometheus.Services.Model;
using Prometheus.Services.Service;

namespace Prometheus.Services.UnitTests
{
    public class CodeGeneratorTests {
        [TestCaseSource(nameof(CodeGeneratorCases))]
        public void CodeGenerator_GeneratesSelectionDeclaration_Correctly(string codeInput, string[] declarations) {
            var extractor = new DataStructureExtractor();
            extractor.Visit(codeInput);
            var generationService = new CodeGenerationService(extractor.DataStructure, new TypeService(extractor.DataStructure));
            var codeGenerator = new CodeGenerator(generationService);
            codeGenerator.Visit(codeInput);

            Console.WriteLine(codeGenerator.CodeOutput);

            foreach (var declaration in declarations)
            {
                Assert.True(codeGenerator.CodeOutput.Contains(declaration));
            }
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

                                                struct node * head = NULL;

                                                void insertFirst() {
                                                   struct node * variable = head;

                                                   if(head->next==variable || (head->next!=head && head->data == variable->data)){
                                                   }
                                                }",
                                                new[] {
                                               "int oldVariableData = variable->data;",
                                               "int oldHeadData = head->data;",
                                               "struct node * oldVariable = variable;",
                                               "struct node * oldHead = head;",
                                               "struct node * oldHeadNext = head->next;"
                                                });
                #endregion
            }
        }

        #endregion
    }
}