public static class ApiEndpoints
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        AuthHandlers.Map(app);
        RoomsHandlers.Map(app);
        BookingsHandlers.Map(app);
        TariffsHandlers.Map(app);
        SystemHandlers.Map(app);
    }
}
