using System.Globalization;

namespace TanukiTarkovMap.Models.Data
{
    /// <summary>
    /// 3D 좌표 위치 데이터 클래스
    /// 게임 내 플레이어 위치를 X, Y, Z 좌표로 표현
    /// </summary>
    public class Position
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public Position(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Position(string x, string y, string z)
        {
            X = float.Parse(x, CultureInfo.InvariantCulture);
            Y = float.Parse(y, CultureInfo.InvariantCulture);
            Z = float.Parse(z, CultureInfo.InvariantCulture);
        }
    }
}
