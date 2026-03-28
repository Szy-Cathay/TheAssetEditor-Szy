namespace GameWorld.Core.Components
{
    public enum ComponentUpdateOrderEnum
    {
        RenderEngine,
        Input,
        Camera,
        NavigationGizmo,
        Animation,

        Gizmo,
        SelectionComponent,
        Default,
    }

    public enum ComponentDrawOrderEnum
    {
        Default,

        RenderEngine,
        Gizmo,
        SelectionComponent,
        NavigationGizmo,
    }
}
