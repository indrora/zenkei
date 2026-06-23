namespace Zenkei.Models;

/// <summary>
/// Yaw (horizontal rotation) and Pitch (vertical angle) in degrees.
/// Used as the PropertyGrid subject for marker positions and initial view,
/// rendered by <see cref="Controls.YawPitchControl"/> via <see cref="PropertyGrid.YawPitchCellFactory"/>.
/// </summary>
public readonly record struct YawPitch(double Yaw, double Pitch);
