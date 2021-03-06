﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VehicleTelemetry {
    /// <summary>
    /// Represents a geographical coordinate.
    /// </summary>
    /// <remarks>
    /// The coordinate system used if standard longitude/latitude/altitude.
    /// Longitude and latitude are given in degrees, negative for western and southern hemispheres.
    /// Base points are the equator and the Greenwich-line.
    /// Altitude is given in meters above see level.
    /// </remarks>
    public class GeoPoint {
        private double lat, lng, alt;

        public GeoPoint(double lat = 0, double lng = 0, double alt = 0) {
            this.lat = lat;
            this.lng = lng;
            this.alt = alt;
        }

        /// <summary>
        /// Latitude of the point (degrees). 
        /// </summary>
        /// <remarks>
        /// Use negative value for southern hemisphere.
        /// Out of range values will be clamped to -90 or 90 degrees.
        /// </remarks>
        public double Lat {
            get {
                return lat;
            }
            set {
                lat = value;
                if (lat < -90)
                    lat = -90;
                else if (lat > 90)
                    lat = 90;
            }
        }

        /// <summary>
        /// Longitude of the point (degrees). 
        /// </summary>
        /// <remarks>
        /// Use negative value for western hemisphere.
        /// Out of range values will be mapped to -180:180 with no loss of information.
        /// </remarks>
        public double Lng {
            get {
                return lng;
            }
            set {
                lng = value;
                if (lng > 180) {
                    lng /= 360;
                    lng = lng - Math.Truncate(lng);
                    lng *= 360;
                    if (lng > 180)
                        lng -= 360;
                }
                else if (lng < -180) {
                    lng /= 360;
                    lng = lng - Math.Truncate(lng);
                    lng *= 360;
                    if (lng < -180)
                        lng -= 360;
                }
            }
        }

        /// <summary>
        /// Altitude of the point in meters.
        /// </summary>
        /// <remarks>
        /// Sea level equals zero.
        /// Altitude cannot be less then -6 378 100m, the earth's radius.
        /// </remarks>
        public double Alt {
            get {
                return alt;
            }
            set {
                alt = value;
                if (alt < -6378100.0) {
                    alt = -6378100.0; // radius of earth
                }
            }
        }


        /// <summary>
        /// Computes distance between two points along a straight 3D line.
        /// </summary>
        /// <param name="p1">The first point.</param>
        /// <param name="p2">The seconds point.</param>
        /// <returns>Distance between the two points.</returns>
        public static double DistanceDirect(GeoPoint p1, GeoPoint p2) {
            double[] p1xyz = p1.ToXYZ();
            double[] p2xyz = p2.ToXYZ();
            double dx = p1xyz[0] - p2xyz[0];
            double dy = p1xyz[1] - p2xyz[1];
            double dz = p1xyz[2] - p2xyz[2];
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>
        /// Computes distance between two points along Earth's surface, considering constant climb rate.
        /// </summary>
        /// <param name="p1">The first point.</param>
        /// <param name="p2">The second point.</param>
        /// <returns>The distance between the two points.</returns>
        public static double DistanceArc(GeoPoint p1, GeoPoint p2) {
            throw new NotImplementedException("not implemented");
        }

        /// <summary>
        /// Translates geographics lat/long/alt coordinates to cartesian coordinates.
        /// </summary>
        /// <returns>A 3-element array containing x,y and z coordinates.</returns>
        private double[] ToXYZ() {
            double r = 6378100.0 + Alt;
            return new double[] {
                r*Math.Cos(Lat / 180 * Math.PI)*Math.Cos(Lng / 180 * Math.PI),
                r*Math.Cos(Lat / 180 * Math.PI)*Math.Sin(Lng / 180 * Math.PI),
                r*Math.Sin(Lat / 180 * Math.PI)
            };
        }
    }
}
