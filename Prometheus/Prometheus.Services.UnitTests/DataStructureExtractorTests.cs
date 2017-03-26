using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Prometheus.Services.Extensions;
using Prometheus.Services.Model;

namespace Prometheus.Services.UnitTests
{
    public class DataStructureExtractorTests
    {
        [TestCaseSource(nameof(DataStructureCases))]
        public void DataStructureExtractor_ExtractsDataStructure_PerformsCorrectly(string codeInput, string[] globalVariables, Structure[] structures, Method[] methods)
        {
            var codeVisitor = new DataStructureExtractor();
            codeVisitor.Visit(codeInput);
            Assert.True(globalVariables.All(x => codeVisitor.DataStructure.HasGlobalVariable(x)));
            Assert.True(structures.All(x => codeVisitor.DataStructure.Structures.Contains(x)));

            foreach (var operation in methods)
            {
                Assert.True(codeVisitor.DataStructure.Operations.Contains(operation));
            }
        }

        [Ignore]
        [TestCaseSource(nameof(RegionCases))]
        public void DataStructureExtractor_ExtractsRegionsForOperations_PerformsCorrectly(string codeInput, Dictionary<string, int> operationRegions)
        {
            var codeVisitor = new DataStructureExtractor();
            codeVisitor.Visit(codeInput);

            foreach (var operationRegion in operationRegions)
            {
                //Assert.AreEqual(operationRegion.Value, codeVisitor.DataStructure.GetValue("_methodInternalCodes").Invoke(operationRegion.Key).Count);
            }
        }

        #region Test Case Sources

        public IEnumerable<TestCaseData> DataStructureCases
        {
            get
            {

                #region First case
                yield return new TestCaseData(@"struct node {
                                                   int data;
                                                   int key;
                                                   struct node *next;
                                                };

                                                int globalFirst;
                                                int globalSecond = 5;
                                                struct node *head = NULL;

                                                void insertFirst(int key, int data) {
                                                   int localFirst;
                                                   int localSecond = 10;
                                                   struct node *localThird = (struct node*) malloc(sizeof(struct node));

                                                   localThird->key = key;
                                                   localThird->data = data;
                                                   localThird->next = head;
                                                   head = localThird;
                                                }

                                                int main() {
                                                }",
                            new[] { "globalFirst", "globalSecond", "head" },
                            new[] { new Structure("node")
                            {
                                Fields = new List<Field>
                                {
                                    new Field("int", "data"),
                                    new Field("int", "key"),
                                    new Field("struct node *", "next")
                                }
                            } },
                            new[]
                            {
                        new Method("insertFirst", new List<Variable>
                            {
                                new Variable("localFirst", "int", "insertFirst") {DependentVariables = new HashSet<string>(), LinksToGlobalState = false},
                                new Variable("localSecond", "int", "insertFirst") {DependentVariables = new HashSet<string>(), LinksToGlobalState = false},
                                new Variable("localThird", "struct node *", "insertFirst") {DependentVariables = new HashSet<string> {"head"}, LinksToGlobalState = true},
                            }
                        ),
                        new Method("main")
                            });
                #endregion

                #region Second case
                yield return new TestCaseData(@"struct node {
                                                   int data;
                                                   int key;
                                                   struct node *next;
                                                };

                                                struct node *head = NULL;
                                                struct node *current = NULL;

                                                void insertFirst(int key, int data) {
                                                   int variable;
                                                   struct node *link = (struct node*) malloc(sizeof(struct node));

                                                   link->key = key;
                                                   link->data = data;
                                                   link->next = head;
                                                   head = link;
                                                }

                                                struct node* deleteFirst() {
                                                   struct node *tempLink = head;
                                                   head = head->next;

                                                   return tempLink;
                                                }

                                                struct node* find(int key) {
                                                   struct node* current = head;

                                                   if(head == NULL) {
                                                      return NULL;
                                                   }

                                                   while(current->key != key) {
                                                      if(current->next == NULL) {
                                                         return NULL;
                                                      } else {
                                                         current = current->next;
                                                      }
                                                   }

                                                   return current;
                                                }

                                                struct node* delete(int key) {
                                                   struct node* current = head;
                                                   struct node* previous = NULL;

                                                   if(head == NULL) {
                                                      return NULL;
                                                   }

                                                   while(current->key != key) {
                                                      if(current->next == NULL) {
                                                         return NULL;
                                                      } else {
                                                         previous = current;
                                                         current = current->next;
                                                      }
                                                   }

                                                   if(current == head) {
                                                      head = head->next;
                                                   } else {
                                                      previous->next = current->next;
                                                   }

                                                   return current;
                                                }

                                                main() {
                                                }",
                            new[] { "head", "current" },
                            new[] { new Structure("node")
                            {
                                Fields = new List<Field>
                                {
                                    new Field("int", "data"),
                                    new Field("int", "key"),
                                    new Field("struct node *", "next")
                                }
                            } },
                            new[]
                            {
                        new Method("insertFirst", new List<Variable>
                            {
                                new Variable("variable", "int", "insertFirst") {DependentVariables = new HashSet<string>(), LinksToGlobalState = false},
                                new Variable("link", "struct node *", "insertFirst") {DependentVariables = new HashSet<string> {"head"}, LinksToGlobalState = true}
                            }
                        ),
                        new Method("deleteFirst", new List<Variable>
                            {
                                new Variable("tempLink", "struct node *", "deleteFirst") {DependentVariables = new HashSet<string> {"head"}, LinksToGlobalState = true}
                            }
                        ),
                        new Method("find", new List<Variable>
                            {
                                new Variable("current", "struct node *", "find") {DependentVariables = new HashSet<string> {"head"}, LinksToGlobalState = true}
                            }
                        ),
                        new Method("delete", new List<Variable>
                            {
                                new Variable("current", "struct node *", "delete") {DependentVariables = new HashSet<string> {"head"}, LinksToGlobalState = true},
                                new Variable("previous", "struct node *", "delete") {DependentVariables = new HashSet<string> {"current"}, LinksToGlobalState = true},
                            }
                        ),
                        new Method("main")
                            });
                #endregion

                #region Third case
                yield return new TestCaseData(@"struct node {
                                                   int data;
                                                   int key;
                                                   struct node *next;
                                                };

                                                struct node *head = NULL;
                                                struct node *current = NULL;

                                                struct node* delete(int key) {
                                                   struct node* current = head;
                                                   struct node* previous = NULL;

                                                   if(head == NULL) {
                                                      return NULL;
                                                   }

                                                   while(current->key != key) {
                                                      if(current->next == NULL) {
                                                         return NULL;
                                                      } else {
                                                         previous = current;
                                                         current = current->next;
                                                      }
                                                   }

                                                   if(current == head) {
                                                      head = head->next;
                                                   } else {
                                                      previous->next = current->next;
                                                   }

                                                   return current;
                                                }

                                                main() {
                                                }",
                            new[] { "head", "current" },
                            new[] { new Structure("node")
                            {
                                Fields = new List<Field>
                                {
                                    new Field("int", "data"),
                                    new Field("int", "key"),
                                    new Field("struct node *", "next")
                                }
                            } },
                            new[]
                            {
                                new Method("delete", new List<Variable>
                                    {
                                        new Variable("current", "struct node *", "delete") {DependentVariables = new HashSet<string> {"head"}, LinksToGlobalState = true},
                                        new Variable("previous", "struct node *", "delete") {DependentVariables = new HashSet<string> {"current"}, LinksToGlobalState = true},
                                    }
                                ),
                                new Method("main")
                            });
                #endregion

                #region Fourth case
                yield return new TestCaseData(@"struct node {
                                                   int data;
                                                   int key;
                                                   struct node *next;
                                                };

                                                struct node *head = NULL;
                                                struct node *current = NULL;

                                                struct node* deleteFirst() {
                                                   struct node *tempLink = head;
                                                   head = head->next;

                                                   return tempLink;
                                                }

                                                main() {
                                                }",
                            new[] { "head", "current" },
                            new[] { new Structure("node")
                            {
                                Fields = new List<Field>
                                {
                                    new Field("int", "data"),
                                    new Field("int", "key"),
                                    new Field("struct node *", "next")
                                }
                            } },
                            new[]
                            {
                                new Method("deleteFirst", new List<Variable>
                                    {
                                        new Variable("tempLink", "struct node *", "deleteFirst") {DependentVariables = new HashSet<string> {"head"}, LinksToGlobalState = true}
                                    }
                                ),
                                new Method("main")
                            });
                #endregion

                #region Fifth case
                yield return new TestCaseData(@"struct node {
                                                   int data;
                                                   int key;
                                                   struct node *next;
                                                   struct node **vector;
                                                };

                                                struct infoStruct {
                                                   int * vectorData;
                                                   struct node *next;
                                                   struct node **vectorPointers;
                                                };

                                                main() {
                                                }",
                            new string[] { },
                            new[] { new Structure("node")
                            {
                                Fields = new List<Field>
                                {
                                    new Field("int", "data"),
                                    new Field("int", "key"),
                                    new Field("struct node *", "next"),
                                    new Field("struct node **", "vector")
                                }
                            },
                            new Structure("infoStruct")
                            {
                                Fields = new List<Field>
                                {
                                    new Field("int *", "vectorData"),
                                    new Field("struct node *", "next"),
                                    new Field("struct node **", "vectorPointers")
                                }
                            },
                            },
                            new[]
                            {
                                new Method("main")
                            });
                #endregion
            }
        }

        public IEnumerable<TestCaseData> RegionCases
        {
            get
            {
                #region First case
                yield return new TestCaseData(@"struct node {
                                                   int data;
                                                   int key;
                                                   struct node *next;
                                                };

                                                struct node *head = NULL;
                                                struct node *current = NULL;

                                                struct node* method_one_if_else() {
                                                   struct node *tempLink = head;
                                                   head = head->next;

                                                   if(head==tempLink) {
                                                        head = tail;
                                                   }
                                                   else {
                                                        head = tail->next;
                                                   }

                                                   return tempLink;
                                                }

                                                main() {
                                                }",
                            new Dictionary<string, int>
                            {
                                {"method_one_if_else", 4},
                            });
                #endregion

                #region Second case
                yield return new TestCaseData(@"struct node {
                                                   int data;
                                                   int key;
                                                   struct node *next;
                                                };

                                                struct node *head = NULL;
                                                struct node *current = NULL;

                                                struct node* method_two_simple_if_else() {
                                                   struct node *tempLink = head;
                                                   head = head->next;

                                                   if(head==tempLink) {
                                                        head = tail;
                                                   }
                                                   else {
                                                        head = tail->next;
                                                   }

                                                   head->next = NULL;

                                                   if(head==tempLink) {
                                                        head = tail;
                                                   }
                                                   else {
                                                        head = tail->next;
                                                   }

                                                   return tempLink;
                                                }

                                                main() {
                                                }",
                            new Dictionary<string, int>
                            {
                                {"method_two_simple_if_else", 7}
                            });
                #endregion

                #region Third case
                yield return new TestCaseData(@"struct node {
                                                   int data;
                                                   int key;
                                                   struct node *next;
                                                };

                                                struct node *head = NULL;
                                                struct node *current = NULL;

                                                struct node* method_one_nested_if() {
                                                   struct node *tempLink = head;
                                                   head = head->next;

                                                   if(head==tempLink) {
                                                        head = tail;

                                                        if(head==tempLink) {
                                                            head = tail;
                                                        }
                                                        else {
                                                            head = tail->next;
                                                        }

                                                        head = tail->next;
                                                   }

                                                   head->next = NULL;

                                                   return tempLink;
                                                }

                                                main() {
                                                }",
                            new Dictionary<string, int>
                            {
                                {"method_one_nested_if", 6}
                            });
                #endregion

                #region Fourth case
                yield return new TestCaseData(@"struct node {
                                                    int data;
                                                    int key;
                                                    struct node *next;
                                                };

                                                struct node *head = NULL;
                                                struct node *current = NULL;

                                                struct node* method_two_nested_if() {
                                                    struct node *tempLink = head;
                                                    head = head->next;

                                                    if(head==tempLink) {
                                                        head = tail;

                                                        if(head==tempLink) {
                                                            head = tail;
                                                        }
                                                        else {
                                                            head = tail->next;
                                                        }

                                                        head = tail->next;
                                                    }

                                                    head = tail;
                                                    tail = NULL;

                                                    if(head==tempLink) {
                                                        head = tail;

                                                        if(head==tempLink) {
                                                            head = tail;
                                                        }
                                                        else {
                                                            head = tail->next;
                                                        }

                                                        head = tail->next;
                                                    }

                                                    head->next = NULL;

                                                    return tempLink;
                                                }
                                                main() {
                                                }",
                            new Dictionary<string, int>
                            {
                                {"method_two_nested_if", 11},
                            });
                #endregion

                #region Fifth case
                yield return new TestCaseData(@"struct node {
                                                    int data;
                                                    int key;
                                                    struct node *next;
                                                };

                                                struct node *head = NULL;
                                                struct node *current = NULL;

                                                struct node* method_two_complex_nested_if() {
                                                    struct node *tempLink = head;
                                                    head = head->next;

                                                    if(head==tempLink) {
                                                        head = tail;

                                                        if(head==tempLink) {
                                                            head = tail;
                                                        }
                                                        else {
                                                            head = tail->next;
                                                        }

                                                        head = tail->next;
                                                    }

                                                    head = tail;

                                                    if(head==tempLink) {
                                                        head = tail;

                                                        if(head==tempLink) {
                                                            head = tail;
                                                        }
                                                        else {
                                                            head = tail->next;
                                                        }

                                                        head = tail->next;
                                                    } else {
                                                        head = tail->next;

                                                        if(head==tempLink) {
                                                            head = tail;
                                                        }

                                                        tail = NULL;
                                                    }

                                                    head->next = NULL;

                                                    return tempLink;
                                                }
                                                main() {
                                                }",
                            new Dictionary<string, int>
                            {
                                {"method_two_complex_nested_if", 14},
                            });
                #endregion*/

                //todo: currently if statements with multiple else if statements are not processed correctly
                #region Sixth case
                yield return new TestCaseData(@"struct node {
                                                   int data;
                                                   int key;
                                                   struct node *next;
                                                };

                                                struct node *head = NULL;
                                                struct node *current = NULL;

                                                struct node* method_if_multiple_elses() {
                                                   struct node *tempLink = head;
                                                   head = head->next;

                                                   if(head==tempLink) {
                                                        head = tail;
                                                   }
                                                   else if (1==1) {
                                                        head = tail->next;
                                                   }
                                                   else {
                                                        head = tail->next->next;
                                                   }

                                                   return tempLink;
                                                }

                                                main() {
                                                }",
                            new Dictionary<string, int>
                            {
                                {"method_if_multiple_elses", 5},
                            });
                #endregion

                #region Seventh case
                yield return new TestCaseData(@"struct node {
                                                   int data;
                                                   int key;
                                                   struct node *next;
                                                };

                                                struct node *head = NULL;
                                                struct node *current = NULL;

                                                struct node* method_if_multiple_elses() {
                                                   struct node *tempLink = head;
                                                   head = head->next;

                                                   if(head==tempLink) {
                                                        head = tail;
                                                   }
                                                   else if (true) {
                                                        head = tail->next;
                                                   }
                                                   else {
                                                        head = tail->next->>next;
                                                   }

                                                   head = tail;

                                                   if(head==tempLink) {
                                                        head = tail;
                                                   }
                                                   else if (true) {
                                                        head = tail->next;
                                                   }
                                                   else {
                                                        head = tail->next->next;
                                                   }

                                                   return tempLink;
                                                }

                                                main() {
                                                }",
                            new Dictionary<string, int>
                            {
                                {"method_if_multiple_elses", 9},
                            });
                #endregion
            }
        }

        #endregion
    }
}