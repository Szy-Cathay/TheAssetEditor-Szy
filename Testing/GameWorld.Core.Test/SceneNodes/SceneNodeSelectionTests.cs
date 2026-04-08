using GameWorld.Core.SceneNodes;
using GameWorld.Core.Rendering.Geometry;
using Microsoft.Xna.Framework;

namespace Testing.GameWorld.Core.SceneNodes
{
    [TestFixture]
    internal class SceneNodeSelectionTests
    {
        #region Test doubles

        /// <summary>
        /// Lightweight test node implementing ISelectable.
        /// Used to verify the ForeachNodeRecursive + ISelectable pattern
        /// that SuperViewViewModel.DisableSelectionOnAllNodes() relies on.
        /// </summary>
        class TestSelectableNode : SceneNode, ISelectable
        {
            public MeshObject Geometry { get; set; }
            public bool IsSelectable { get; set; } = true;

            public override ISceneNode CreateCopyInstance() => new TestSelectableNode();
            public override void CopyInto(ISceneNode target) => base.CopyInto(target);
        }

        #endregion

        #region Bug 2: DisableSelectionOnAllNodes pattern

        [Test]
        public void ForeachNodeRecursive_SetsIsSelectableFalse_OnAllISelectableNodes()
        {
            // Arrange - simulate the scene tree in SuperView
            var root = new GroupNode("MainNode");
            var mesh1 = new TestSelectableNode { Name = "body_mesh", IsSelectable = true };
            var mesh2 = new TestSelectableNode { Name = "weapon_mesh", IsSelectable = true };
            var mesh3 = new TestSelectableNode { Name = "shield_mesh", IsSelectable = true };

            root.AddObject(mesh1);
            root.AddObject(mesh2);
            root.AddObject(mesh3);

            // Act - same pattern as SuperViewViewModel.DisableSelectionOnAllNodes()
            root.ForeachNodeRecursive((node) =>
            {
                if (node is ISelectable selectable)
                    selectable.IsSelectable = false;
            });

            // Assert
            Assert.That(mesh1.IsSelectable, Is.False, "body_mesh should be non-selectable");
            Assert.That(mesh2.IsSelectable, Is.False, "weapon_mesh should be non-selectable");
            Assert.That(mesh3.IsSelectable, Is.False, "shield_mesh should be non-selectable");
        }

        [Test]
        public void ForeachNodeRecursive_SetsIsSelectableFalse_InNestedTree()
        {
            // Arrange - simulate nested tree: MainNode > SlotNode > mesh nodes
            var mainNode = new GroupNode("MainNode");
            var slotNode = new GroupNode("WeaponSlot");
            var meshNode = new TestSelectableNode { Name = "weapon", IsSelectable = true };

            slotNode.AddObject(meshNode);
            mainNode.AddObject(slotNode);

            // Act
            mainNode.ForeachNodeRecursive((node) =>
            {
                if (node is ISelectable selectable)
                    selectable.IsSelectable = false;
            });

            // Assert - deep nesting should also be handled
            Assert.That(meshNode.IsSelectable, Is.False, "Nested mesh should be non-selectable");
        }

        [Test]
        public void ForeachNodeRecursive_DoesNotCrash_WhenNoISelectableNodes()
        {
            // Arrange - tree with only GroupNodes (no ISelectable)
            var root = new GroupNode("Root");
            var child = new GroupNode("Child");
            root.AddObject(child);

            // Act & Assert - should not throw
            Assert.DoesNotThrow(() =>
            {
                root.ForeachNodeRecursive((node) =>
                {
                    if (node is ISelectable selectable)
                        selectable.IsSelectable = false;
                });
            });
        }

        [Test]
        public void ForeachNodeRecursive_PreservesOtherNodeProperties()
        {
            // Arrange
            var root = new GroupNode("Root");
            var meshNode = new TestSelectableNode
            {
                Name = "test_mesh",
                IsSelectable = true,
                IsVisible = true,
                IsEditable = true
            };
            root.AddObject(meshNode);

            // Act - only change IsSelectable
            root.ForeachNodeRecursive((node) =>
            {
                if (node is ISelectable selectable)
                    selectable.IsSelectable = false;
            });

            // Assert - other properties should remain unchanged
            Assert.That(meshNode.IsSelectable, Is.False);
            Assert.That(meshNode.IsVisible, Is.True, "IsVisible should not be changed");
            Assert.That(meshNode.IsEditable, Is.True, "IsEditable should not be changed");
            Assert.That(meshNode.Name, Is.EqualTo("test_mesh"), "Name should not be changed");
        }

        [Test]
        public void ForeachNodeRecursive_HandlesEmptyTree()
        {
            // Arrange - single node, no children
            var root = new GroupNode("EmptyRoot");

            // Act & Assert - should not throw
            Assert.DoesNotThrow(() =>
            {
                root.ForeachNodeRecursive((node) =>
                {
                    if (node is ISelectable selectable)
                        selectable.IsSelectable = false;
                });
            });
        }

        #endregion
    }
}
