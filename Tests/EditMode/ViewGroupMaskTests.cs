using NUnit.Framework;

namespace Sentinal.Tests
{
    public class ViewGroupMaskTests
    {
        [Test]
        public void MixedXorOperatorsPerformXor()
        {
            var mask = new ViewGroupMask(0b1010);

            Assert.That((mask ^ 0b1100).Value, Is.EqualTo(0b0110));
            Assert.That((0b1100 ^ mask).Value, Is.EqualTo(0b0110));
        }

        [Test]
        public void NothingDoesNotOverlapDefaultOrEverything()
        {
            Assert.That((ViewGroupMask.Nothing & 1), Is.EqualTo(ViewGroupMask.Nothing));
            Assert.That((ViewGroupMask.Nothing & ViewGroupMask.Everything), Is.EqualTo(ViewGroupMask.Nothing));
        }
    }
}
