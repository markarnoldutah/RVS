namespace RVS.Domain.Integrations;

/// <summary>
/// One of the customer's photos, as bytes, handed to <see cref="IPreliminaryAssessmentService"/>
/// so the assessment can read data plates, fault codes and visible condition off it (issue #772).
/// The bytes are the normalised blob the packet PDF embeds: one download feeds both.
/// </summary>
/// <param name="AttachmentId">The attachment the photo is stored as; findings cite it.</param>
/// <param name="ContentType">The stored content type, e.g. <c>image/jpeg</c>.</param>
/// <param name="Bytes">The image bytes.</param>
public sealed record AssessmentPhoto(string AttachmentId, string ContentType, byte[] Bytes);
