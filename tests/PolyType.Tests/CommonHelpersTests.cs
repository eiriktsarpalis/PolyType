namespace PolyType.Tests;

public static class CommonHelpersTests
{
    [Theory]
    [MemberData(nameof(GetDirectedAcyclicGraphs))]
    public static void TraverseGraphWithTopologicalSort_VisitsParentsBeforeChildren(int[][] graph)
    {
        int[] sortedNodes = CommonHelpers.TraverseGraphWithTopologicalSort(0, node => graph[node]);

        Assert.Equal(Enumerable.Range(0, graph.Length), sortedNodes.OrderBy(node => node));
        for (int node = 0; node < graph.Length; node++)
        {
            foreach (int child in graph[node])
            {
                Assert.True(Array.IndexOf(sortedNodes, node) < Array.IndexOf(sortedNodes, child));
            }
        }
    }

    public static IEnumerable<object[]> GetDirectedAcyclicGraphs()
    {
        yield return [new int[][] { [] }];
        yield return [new int[][] { [1], [] }];
        yield return [new int[][] { [1], [2], [] }];
        yield return [new int[][] { [1, 2], [3], [3], [] }];
        yield return [new int[][] { [1, 2], [3], [], [4], [] }];
    }
}
