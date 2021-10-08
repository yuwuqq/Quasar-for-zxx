namespace Quasar.Client.IpGeoLocation
{
    /// <summary>
    /// 存储IP地理定位信息。
    /// </summary>
    public class GeoInformation
    {
        public string IpAddress { get; set; }
        public string Country { get; set; }
        public string CountryCode { get; set; }
        public string Timezone { get; set; }
        public string Asn { get; set; }
        public string Isp { get; set; }
        public int ImageIndex { get; set; }
    }
}
