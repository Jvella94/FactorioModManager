using FactorioModManager;

namespace FMM.Tests.ServicesTests
{
    public class DependencyHelperTests
    {
        [Theory]
        [InlineData("base", "base")]
        [InlineData("? base >= 1.0", "base")]
        [InlineData("(?) base", "base")]
        [InlineData("! some-mod < 2.0", "some-mod")]
        [InlineData("some-mod >= 0.5.0", "some-mod")]
        [InlineData("complex mod name >= 1.2", "complex mod name")]
        [InlineData("~ some-mod", "some-mod")]
        [InlineData("(?)complex mod name>=1.0", "complex mod name")]
        public void ParseDependency_ExtractsName(string input, string expected)
        {
            var actual = Constants.DependencyHelper.ExtractDependencyName(input);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("base >= 1.1", "base", ">=", "1.1")]
        [InlineData("? some-mod <= 2.0", "some-mod", "<=", "2.0")]
        [InlineData("some-mod", "some-mod", null, null)]
        [InlineData("complex mod name >= 1.2.3", "complex mod name", ">=", "1.2.3")]
        public void ParseDependency_ReturnsComponents(string input, string name, string? op, string? ver)
        {
            var parsed = Constants.DependencyHelper.ParseDependency(input);
            Assert.NotNull(parsed);
            Assert.Equal(name, parsed.Value.Name);
            Assert.Equal(op, parsed.Value.VersionOperator);
            Assert.Equal(ver, parsed.Value.Version);
        }
    }
}