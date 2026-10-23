using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class ClientAuditPrefixQualityTests
{
    [Theory]
    [InlineData("; 注释", ClientTextCjkDebtCategory.Comment)]
    [InlineData("\t# 注释", ClientTextCjkDebtCategory.Comment)]
    [InlineData("  * 注释", ClientTextCjkDebtCategory.Comment)]
    [InlineData("// 注释", ClientTextCjkDebtCategory.Comment)]
    [InlineData("-- 注释", ClientTextCjkDebtCategory.Comment)]
    [InlineData("[Section_12]// 注释", ClientTextCjkDebtCategory.Comment)]
    [InlineData("[中文]// 注释", ClientTextCjkDebtCategory.VisibleText)]
    [InlineData("[Section] Text=中文", ClientTextCjkDebtCategory.VisibleText)]
    [InlineData("[Section]// English", ClientTextCjkDebtCategory.VisibleText)]
    [InlineData("[Section]", ClientTextCjkDebtCategory.VisibleText)]
    [InlineData("[", ClientTextCjkDebtCategory.VisibleText)]
    [InlineData("", ClientTextCjkDebtCategory.VisibleText)]
    [InlineData(" \t", ClientTextCjkDebtCategory.VisibleText)]
    [InlineData("Name=中文; tail", ClientTextCjkDebtCategory.VisibleText)]
    [InlineData("Name='; 中文'", ClientTextCjkDebtCategory.VisibleText)]
    [InlineData("Name=English ; 注释", ClientTextCjkDebtCategory.Comment)]
    [InlineData("＃ 中文", ClientTextCjkDebtCategory.VisibleText)]
    [InlineData("； 中文", ClientTextCjkDebtCategory.VisibleText)]
    public void CommentAndSectionPrefixesRetainOrdinalClassification(
        string line, ClientTextCjkDebtCategory expected)
    {
        var actual = ClientTextCjkDebtClassifier.Classify("ini/Labels.ini", line);

        Assert.Equal(expected, actual);
        Assert.Equal(expected == ClientTextCjkDebtCategory.VisibleText,
            ClientTextCjkDebtClassifier.IsActionable(actual));
    }

    [Theory]
    [InlineData("ini/pinyindata.ini", ClientTextCjkDebtCategory.SupportData)]
    [InlineData("Users/123.ini", ClientTextCjkDebtCategory.PlayerState)]
    public void PathCategoriesRemainHigherPriorityThanCommentPrefix(
        string relativePath, ClientTextCjkDebtCategory expected)
    {
        Assert.Equal(expected, ClientTextCjkDebtClassifier.Classify(relativePath, "# 注释"));
    }
}
