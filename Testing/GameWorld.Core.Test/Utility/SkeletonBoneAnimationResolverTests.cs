using GameWorld.Core.Animation;
using GameWorld.Core.SceneNodes;
using GameWorld.Core.Utility;
using Microsoft.Xna.Framework;
using Shared.GameFormats.Animation;
using Shared.GameFormats.RigidModel.Transforms;

namespace Testing.GameWorld.Core.Utility
{
    [TestFixture]
    internal class SkeletonBoneAnimationResolverTests
    {
        #region Test helpers

        /// <summary>
        /// Creates a minimal GameSkeleton with known bone positions for testing.
        /// Root bone (index 0) at origin, child bones offset on X axis.
        /// </summary>
        static GameSkeleton CreateTestSkeleton(AnimationPlayer player, int boneCount = 2)
        {
            var animFile = new AnimationFile();
            animFile.Header = new AnimationFile.AnimationHeader { SkeletonName = "TestSkeleton" };
            animFile.Bones = new AnimationFile.BoneInfo[boneCount];

            for (var i = 0; i < boneCount; i++)
            {
                animFile.Bones[i] = new AnimationFile.BoneInfo
                {
                    Name = $"bone_{i}",
                    ParentId = i == 0 ? -1 : 0
                };
            }

            var frame = new AnimationFile.Frame();
            for (var i = 0; i < boneCount; i++)
            {
                frame.Transforms.Add(new RmvVector3 { X = i * 10.0f, Y = 0, Z = 0 });
                frame.Quaternion.Add(new RmvVector4(0, 0, 0, 1));
            }

            var part = new AnimationFile.AnimationPart();
            part.DynamicFrames.Add(frame);
            animFile.AnimationParts.Add(part);

            return new GameSkeleton(animFile, player);
        }

        class TestSkeletonProvider : ISkeletonProvider
        {
            public GameSkeleton Skeleton { get; }
            public TestSkeletonProvider(GameSkeleton skeleton) => Skeleton = skeleton;
        }

        #endregion

        #region Bug 3b: GetWorldTransformIfAnimating should work when paused

        [Test]
        public void GetWorldTransformIfAnimating_ReturnsBoneTransform_WhenEnabledButPaused()
        {
            // Arrange - Bug 3b: weapon must follow bone position even when animation is paused
            var player = new AnimationPlayer();
            var skeleton = CreateTestSkeleton(player, boneCount: 2);
            var provider = new TestSkeletonProvider(skeleton);

            player.IsEnabled = true;
            player.Pause(); // IsPlaying = false

            var boneIndex = skeleton.GetBoneIndexByName("bone_1");
            var resolver = new SkeletonBoneAnimationResolver(provider, boneIndex);

            // Act
            var result = resolver.GetWorldTransformIfAnimating();

            // Assert - bone_1 is at X=10, should NOT be Identity
            Assert.That(result, Is.Not.EqualTo(Matrix.Identity), "Paused animation should still return bone transform");
            Assert.That(result.Translation.X, Is.EqualTo(10.0f).Within(0.001f));
        }

        [Test]
        public void GetWorldTransformIfAnimating_ReturnsBoneTransform_WhenEnabledAndPlaying()
        {
            // Arrange - baseline: playing animation works
            var player = new AnimationPlayer();
            var skeleton = CreateTestSkeleton(player, boneCount: 2);
            var provider = new TestSkeletonProvider(skeleton);

            player.IsEnabled = true;
            // IsPlaying defaults to true

            var boneIndex = skeleton.GetBoneIndexByName("bone_1");
            var resolver = new SkeletonBoneAnimationResolver(provider, boneIndex);

            // Act
            var result = resolver.GetWorldTransformIfAnimating();

            // Assert
            Assert.That(result, Is.Not.EqualTo(Matrix.Identity));
            Assert.That(result.Translation.X, Is.EqualTo(10.0f).Within(0.001f));
        }

        #endregion

        #region Edge cases: conditions that should return Identity

        [Test]
        public void GetWorldTransformIfAnimating_ReturnsIdentity_WhenPlayerDisabled()
        {
            // Arrange
            var player = new AnimationPlayer();
            var skeleton = CreateTestSkeleton(player, boneCount: 2);
            var provider = new TestSkeletonProvider(skeleton);

            player.IsEnabled = false;

            var boneIndex = skeleton.GetBoneIndexByName("bone_1");
            var resolver = new SkeletonBoneAnimationResolver(provider, boneIndex);

            // Act
            var result = resolver.GetWorldTransformIfAnimating();

            // Assert
            Assert.That(result, Is.EqualTo(Matrix.Identity), "Disabled player should return Identity");
        }

        [Test]
        public void GetWorldTransformIfAnimating_ReturnsIdentity_WhenBoneIndexIsMinusOne()
        {
            // Arrange - bone not found in skeleton
            var player = new AnimationPlayer();
            var skeleton = CreateTestSkeleton(player, boneCount: 2);
            var provider = new TestSkeletonProvider(skeleton);

            player.IsEnabled = true;

            var resolver = new SkeletonBoneAnimationResolver(provider, boneIndex: -1);

            // Act
            var result = resolver.GetWorldTransformIfAnimating();

            // Assert
            Assert.That(result, Is.EqualTo(Matrix.Identity), "Invalid bone index should return Identity");
        }

        [Test]
        public void GetWorldTransformIfAnimating_ReturnsIdentity_WhenSkeletonIsNull()
        {
            // Arrange
            var provider = new TestSkeletonProvider(null);
            var resolver = new SkeletonBoneAnimationResolver(provider, boneIndex: 0);

            // Act
            var result = resolver.GetWorldTransformIfAnimating();

            // Assert
            Assert.That(result, Is.EqualTo(Matrix.Identity), "Null skeleton should return Identity");
        }

        #endregion

        #region Bug 3b: GetTransformIfAnimating should also work when paused

        [Test]
        public void GetTransformIfAnimating_ReturnsBoneTransform_WhenEnabledButPaused()
        {
            // Arrange - same fix applies to GetTransformIfAnimating
            var player = new AnimationPlayer();
            var skeleton = CreateTestSkeleton(player, boneCount: 2);
            var provider = new TestSkeletonProvider(skeleton);

            player.IsEnabled = true;
            player.Pause();

            var boneIndex = skeleton.GetBoneIndexByName("bone_1");
            var resolver = new SkeletonBoneAnimationResolver(provider, boneIndex);

            // Act
            var result = resolver.GetTransformIfAnimating();

            // Assert
            Assert.That(result, Is.Not.EqualTo(Matrix.Identity), "GetTransformIfAnimating should also work when paused");
        }

        #endregion

        #region GetWorldTransform (unconditional) always returns transform

        [Test]
        public void GetWorldTransform_ReturnsBoneTransform_RegardlessOfPlayerState()
        {
            // Arrange - unconditional version ignores player state
            var player = new AnimationPlayer();
            var skeleton = CreateTestSkeleton(player, boneCount: 2);
            var provider = new TestSkeletonProvider(skeleton);

            player.IsEnabled = false;
            player.Pause();

            var boneIndex = skeleton.GetBoneIndexByName("bone_1");
            var resolver = new SkeletonBoneAnimationResolver(provider, boneIndex);

            // Act
            var result = resolver.GetWorldTransform();

            // Assert - unconditional version always returns bone transform
            Assert.That(result, Is.Not.EqualTo(Matrix.Identity));
            Assert.That(result.Translation.X, Is.EqualTo(10.0f).Within(0.001f));
        }

        #endregion
    }
}
