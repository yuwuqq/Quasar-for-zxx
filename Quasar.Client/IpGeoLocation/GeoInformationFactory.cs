using System;

namespace Quasar.Client.IpGeoLocation
{
    /// <summary>
    /// 工厂在<see cref="MINIMUM_VALID_TIME"/>分钟内检索并缓存最后的IP地理定位信息。
    /// </summary>
    public static class GeoInformationFactory
    {
        /// <summary>
        /// 用于获取广域网IP地址的地理定位信息的检索器。
        /// </summary>
        private static readonly GeoInformationRetriever Retriever = new GeoInformationRetriever();

        /// <summary>
        /// 用于缓存最新的IP地理定位信息。
        /// </summary>
        private static GeoInformation _geoInformation;

        /// <summary>
        /// 最后一次成功检索位置的时间。
        /// </summary>
        private static DateTime _lastSuccessfulLocation = new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// 一个成功的IP地理定位检索的最小有效时间。
        /// </summary>
        private const int MINIMUM_VALID_TIME = 60 * 12;

        /// <summary>
        /// 获取IP地理定位信息，如果超过<see cref="MINIMUM_VALID_TIME"/>分钟，可以是缓存的，也可以是刚检索到的。
        /// </summary>
        /// <returns>最新的IP地理定位信息。</returns>
        public static GeoInformation GetGeoInformation()
        {
            var passedTime = new TimeSpan(DateTime.UtcNow.Ticks - _lastSuccessfulLocation.Ticks);

            if (_geoInformation == null || passedTime.TotalMinutes > MINIMUM_VALID_TIME)
            {
                _geoInformation = Retriever.Retrieve();
                _lastSuccessfulLocation = DateTime.UtcNow;
            }

            return _geoInformation;
        }
    }
}
