using System;
using System.Collections.Generic;
using NUnit.Framework;
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

                #region Second case
                yield return new TestCaseData(@"struct node {
                                                   int data;
                                                   struct node *next;
                                                };

                                                struct node * head = NULL;
                                                struct node * tail = NULL;

                                                void insertFirst() {
                                                   struct node * headVar = head;
                                                   struct node * tailVar = tail;

                                                   if(headVar->data == tailVar->data){
                                                   }
                                                }",
                                                new[] {
                                               "int oldHeadVarData = headVar->data;",
                                               "int oldTailVarData = tailVar->data;"
                                                });
                #endregion

                #region Third case
                yield return new TestCaseData(@"struct node {
                                                   int data;
                                                   struct node *next;
                                                };

                                                struct node * head = NULL;
                                                struct node * tail = NULL;

                                                void insertFirst() {
                                                   struct node * headVar = head;
                                                   struct node * tailVar = tail;

                                                   if(headVar->data == tailVar->data){
                                                   }

                                                   if(headVar->data != tailVar->data){
                                                   }
                                                }",
                                                new[] {
                                               "int oldHeadVarData = headVar->data;",
                                               "int oldTailVarData = tailVar->data;",
                                               "oldHeadVarData = headVar->data;",
                                               "oldTailVarData = tailVar->data;"
                                                });
                #endregion
            }
        }

        #endregion
    }
}