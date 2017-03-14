using System;
using System.Collections.Generic;
using NUnit.Framework;
using Prometheus.Services.Service;

namespace Prometheus.Services.UnitTests
{
    public class CodeGeneratorTests {
        [TestCaseSource(nameof(EqualitySelectionCases))]
        public void CodeGenerator_GeneratesEqualitySelectionDeclarations_Correctly(string codeInput, string[] declarations) {

            var extractor = new DataStructureExtractor();
            extractor.Visit(codeInput);
            var typeService = new TypeService(extractor.DataStructure);
            var generationService = new RelationService(extractor.DataStructure, typeService);
            var codeGenerator = new CodeGenerator(extractor.DataStructure, generationService);
            codeGenerator.Visit(codeInput);

            Console.WriteLine(codeInput);
            Console.WriteLine("====================");
            Console.WriteLine(codeGenerator.CodeOutput);

            foreach (var declaration in declarations)
            {
                Assert.True(codeGenerator.CodeOutput.Contains(declaration));
            }
        }

        [TestCaseSource(nameof(ComparisonSelectionCases))]
        public void CodeGenerator_GeneratesComparisonSelectionDeclarations_Correctly(string codeInput, string[] declarations) {
            var extractor = new DataStructureExtractor();
            extractor.Visit(codeInput);
            var typeService = new TypeService(extractor.DataStructure);
            var generationService = new RelationService(extractor.DataStructure, typeService);
            var codeGenerator = new CodeGenerator(extractor.DataStructure, generationService);
            codeGenerator.Visit(codeInput);

            Console.WriteLine(codeInput);
            Console.WriteLine("====================");
            Console.WriteLine(codeGenerator.CodeOutput);

            foreach (var declaration in declarations) {
                Assert.True(codeGenerator.CodeOutput.Contains(declaration));
            }
        }

        [TestCaseSource(nameof(QueueGenerationCases))]
        public void CodeGenerator_GeneratesQueueDeclarations_Correctly(string codeInput, string[] declarations) {
            var extractor = new DataStructureExtractor();
            extractor.Visit(codeInput);
            var typeService = new TypeService(extractor.DataStructure);
            var generationService = new RelationService(extractor.DataStructure, typeService);
            var codeGenerator = new CodeGenerator(extractor.DataStructure, generationService);
            codeGenerator.Visit(codeInput);

            Console.WriteLine(codeInput);
            Console.WriteLine("====================");
            Console.WriteLine(codeGenerator.CodeOutput);

            foreach (var declaration in declarations) {
                Assert.True(codeGenerator.CodeOutput.Contains(declaration));
            }
        }

        #region Test Case Sources

        public IEnumerable<TestCaseData> EqualitySelectionCases
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
                                               "struct node * oldVariable = variable;",
                                               "struct node * oldHead = head;",
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
                                               "struct node * oldHeadVar = headVar;",
                                               "struct node * oldTailVar = tailVar;"
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
                                               "struct node * oldHeadVar = headVar;",
                                               "struct node * oldTailVar = tailVar;",
                                               "oldHeadVar = headVar;",
                                               "oldTailVar = tailVar;"
                                                });
                #endregion

                #region Fourth case
                yield return new TestCaseData(@"struct node {
                                                   int data;
                                                   struct node *next;
                                                };

                                                struct node * head = NULL;
                                                struct node * tail = NULL;

                                                void enqueue(int value) {
                                                    struct node* temp = (struct node*)malloc(sizeof(struct node));
                                                    temp->data = value;
                                                    temp->next = NULL;

                                                    if (head->next == tail->next && tail->data == head->next->data) {
                                                        return;
                                                    }
                                                }",
                                                new[] {
                                               "struct node * oldHeadNext = head->next;",
                                               "struct node * oldTailNext = tail->next;",
                                               "struct node * oldTail = tail;",
                                               "oldHeadNext = head->next;"
                                                });
                #endregion
            }
        }

        public IEnumerable<TestCaseData> ComparisonSelectionCases
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

                                                   if(head->next->data > variable->data){
                                                   }
                                                }",
                                                new[] {
                                               "struct node * oldVariable = variable;",
                                               "struct node * oldHeadNext = head->next;"
                                                });
                #endregion

                #region Second case
                yield return new TestCaseData(@"struct node {
                                                   int data;
                                                   struct node *next;
                                                };

                                                struct node * head = NULL;

                                                void insertFirst() {
                                                   struct node * variable = head;

                                                   if(head->next->data > variable->data && head->next == variable){
                                                   }
                                                }",
                                                new[] {
                                               "struct node * oldVariable = variable;",
                                               "struct node * oldHeadNext = head->next;",
                                               "struct node * oldHeadNext = head->next;",
                                               "struct node * oldVariable = variable;",
                                                });
                #endregion

                #region Third case
                yield return new TestCaseData(@"struct node {
                                                   int data;
                                                   struct node *next;
                                                };

                                                 struct node * head = NULL;

                                                void insertFirst() {
                                                   struct node * variable = head;

                                                   if(head->next->data > variable->data && head->next == variable || head->data < variable->data){
                                                   }
                                                }",
                                                new[] {
                                               "struct node * oldVariable = variable;",
                                               "struct node * oldHeadNext = head->next;",
                                               "struct node * oldHeadNext = head->next;",
                                               "struct node * oldVariable = variable;",
                                               "oldVariable = variable;",
                                                });
                #endregion
            }
        }

        public IEnumerable<TestCaseData> QueueGenerationCases
        {
            get
            {
                #region comm
                /*#region First case
                yield return new TestCaseData(@"struct node {
                                                   int data;
                                                   struct node *next;
                                                };

                                                struct node * head = NULL;
                                                struct node * tail = NULL;

                                                void enqueue(int data) {
                                                    struct node* temp = (struct node*)malloc(sizeof(struct node));

                                                    temp->data = data;
                                                    temp->next = NULL;

                                                    if (head == NULL && tail == NULL){
                                                        head = temp;
                                                        tail = temp;
                                                        return;
                                                    }
                                                    tail->next = temp;
                                                    tail = temp;
                                                }

                                                int dequeue() {
                                                    struct node* temp = head;

                                                    if(head == NULL) {
                                                        printf(""Queue is Empty"");
                                                        return;
                                                    }

                                                    int result = temp->data;

                                                    if (head == tail) {
                                                        head = tail = NULL;
                                                        return result;
                                                    }

                                                    head = head->next;
                                                    return result;
                                                }
                                                ",
                                                new[] {
                                               "struct node * oldHead = head;",
                                               "struct node * oldTail = tail;",
                                                });
                #endregion

                #region Second case
                yield return new TestCaseData(@"struct node {
                                                   int data;
                                                   struct node *next;
                                                };

                                                struct node * head = NULL;
                                                struct node * tail = NULL;

                                                void enqueue(int value) {
                                                    struct node* temp = (struct node*)malloc(sizeof(struct node));
                                                    temp->data = value;
                                                    temp->next = NULL;

                                                    if (head == NULL && tail == NULL) {
                                                        head = temp;
                                                        tail = temp;
                                                        return;
                                                    }

                                                    tail->next = temp;
                                                    tail = temp;
                                                }",
                                                new[] {
                                               "struct node * oldHead = head;",
                                               "struct node * oldTail = tail;",
                                                });
                #endregion

                #region Third case
                yield return new TestCaseData(@"
                                                struct node {
                                                   int data;
                                                   struct node *next;
                                                };

                                                struct node * head = NULL;
                                                struct node * tail = NULL;

                                                int dequeue() {
                                                    struct node* temp = head;

                                                    if(head == NULL) {
                                                        printf(""Queue is Empty"");
                                                        return;
                                                    }

                                                    int result = temp->data;

                                                    if(head == tail) {
                                                        head = tail = NULL;
                                                        return result;
                                                    }

                                                    head = head->next;
                                                    return result;
                                                }
                                                ",
                                                new[] {
                                               "struct node * oldHead = head;",
                                               "struct node * oldTail = tail;",
                                                });
                #endregion*/

                #endregion

                #region First case
                yield return new TestCaseData(@"struct node {
                                                   int data;
                                                   struct node *next;
                                                };

                                                struct node * head = NULL;
                                                struct node * tail = NULL;

                                                void enqueue(int data) {
                                                    struct node* temp = (struct node*)malloc(sizeof(struct node));

                                                    temp->data = data;
                                                    temp->next = NULL;

                                                    if (head == NULL && tail == NULL) {
                                                        head = temp;
                                                        tail = temp;
                                                        return;
                                                    }

                                                    tail->next = temp;
                                                    tail = temp;
                                                }
                                                ",
                                                new[] {
                                               "struct node * oldHead = head;",
                                               "struct node * oldTail = tail;",
                                                });
                #endregion
            }
        }

        #endregion
    }
}