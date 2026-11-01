using System.Text.Json.Serialization;

namespace APIHandler.Models.GroupReservation;

public record SubmitGroupReservationRequest([property: JsonPropertyName("reservationId")] string ReservationId);

public record GroupOrderItemResponse(
	[property: JsonPropertyName("dishId")] string DishId,
	[property: JsonPropertyName("quantity")] int Quantity);