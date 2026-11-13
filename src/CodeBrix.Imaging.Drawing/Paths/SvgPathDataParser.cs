using System;
using System.Globalization;

namespace CodeBrix.Imaging.Drawing.Paths;

//Reads SVG path data (SVG 1.1 / SVG 2 path grammar) into a geometry
internal sealed class SvgPathDataParser
{
    private readonly string _text;
    private int _position;

    private SvgPathDataParser(string text)
    {
        _text = text;
    }

    internal static PathGeometry Parse(string text) => new SvgPathDataParser(text).Run();

    private PathGeometry Run()
    {
        var builder = new PathGeometryBuilder();
        char command = '\0';
        char previous = '\0';
        PointF current = new PointF(0f, 0f);
        PointF start = current;
        PointF lastControl = current;
        bool haveContour = false;

        SkipSeparators();
        while (_position < _text.Length)
        {
            char c = _text[_position];
            if (IsCommand(c))
            {
                command = c;
                _position++;
            }
            else if (command == '\0')
            {
                throw Error("path data must start with a command");
            }
            else if (command == 'Z' || command == 'z')
            {
                throw Error("a number cannot follow Z");
            }
            //otherwise: the previous command repeats (a repeated move becomes a line)

            bool relative = Char.IsLower(command);
            char kind = Char.ToUpperInvariant(command);
            if (previous == '\0' && kind != 'M') { throw Error("path data must start with a move (M or m)"); }
            switch (kind)
            {
                case 'M':
                {
                    PointF p = ReadPoint(relative, current);
                    builder.MoveTo(p);
                    current = p;
                    start = p;
                    haveContour = true;
                    //Further pairs after a move are lines
                    command = relative ? 'l' : 'L';
                    lastControl = current;
                    break;
                }
                case 'L':
                {
                    PointF p = ReadPoint(relative, current);
                    EnsureContour(builder, ref haveContour, current);
                    builder.LineTo(p);
                    current = p;
                    lastControl = current;
                    break;
                }
                case 'H':
                {
                    float x = ReadNumber();
                    var p = new PointF(relative ? current.X + x : x, current.Y);
                    EnsureContour(builder, ref haveContour, current);
                    builder.LineTo(p);
                    current = p;
                    lastControl = current;
                    break;
                }
                case 'V':
                {
                    float y = ReadNumber();
                    var p = new PointF(current.X, relative ? current.Y + y : y);
                    EnsureContour(builder, ref haveContour, current);
                    builder.LineTo(p);
                    current = p;
                    lastControl = current;
                    break;
                }
                case 'C':
                {
                    PointF c1 = ReadPoint(relative, current);
                    PointF c2 = ReadPoint(relative, current);
                    PointF p = ReadPoint(relative, current);
                    EnsureContour(builder, ref haveContour, current);
                    builder.CubicTo(c1, c2, p);
                    lastControl = c2;
                    current = p;
                    break;
                }
                case 'S':
                {
                    PointF c1 = IsKind(previous, 'C', 'S') ? Reflect(lastControl, current) : current;
                    PointF c2 = ReadPoint(relative, current);
                    PointF p = ReadPoint(relative, current);
                    EnsureContour(builder, ref haveContour, current);
                    builder.CubicTo(c1, c2, p);
                    lastControl = c2;
                    current = p;
                    break;
                }
                case 'Q':
                {
                    PointF c1 = ReadPoint(relative, current);
                    PointF p = ReadPoint(relative, current);
                    EnsureContour(builder, ref haveContour, current);
                    builder.QuadraticTo(c1, p);
                    lastControl = c1;
                    current = p;
                    break;
                }
                case 'T':
                {
                    PointF c1 = IsKind(previous, 'Q', 'T') ? Reflect(lastControl, current) : current;
                    PointF p = ReadPoint(relative, current);
                    EnsureContour(builder, ref haveContour, current);
                    builder.QuadraticTo(c1, p);
                    lastControl = c1;
                    current = p;
                    break;
                }
                case 'A':
                {
                    float rx = ReadNumber();
                    float ry = ReadNumber();
                    float rotation = ReadNumber();
                    bool large = ReadFlag();
                    bool sweep = ReadFlag();
                    PointF p = ReadPoint(relative, current);
                    EnsureContour(builder, ref haveContour, current);
                    builder.ArcTo(rx, ry, rotation, large, sweep, p);
                    current = p;
                    lastControl = current;
                    break;
                }
                case 'Z':
                {
                    builder.Close();
                    current = start;
                    lastControl = current;
                    haveContour = false;
                    break;
                }
            }
            previous = kind;
            SkipSeparators();
        }
        return builder.Build();
    }

    //A drawing command after Z continues from the closed contour's start, in a new contour
    private static void EnsureContour(PathGeometryBuilder builder, ref bool haveContour, PointF current)
    {
        if (haveContour) { return; }
        builder.MoveTo(current);
        haveContour = true;
    }

    private static bool IsKind(char previous, char a, char b) => previous == a || previous == b;

    private static PointF Reflect(PointF control, PointF about)
        => new PointF((2f * about.X) - control.X, (2f * about.Y) - control.Y);

    private static bool IsCommand(char c) => "MmLlHhVvCcSsQqTtAaZz".IndexOf(c) >= 0;

    private PointF ReadPoint(bool relative, PointF current)
    {
        float x = ReadNumber();
        float y = ReadNumber();
        return relative ? new PointF(current.X + x, current.Y + y) : new PointF(x, y);
    }

    private bool ReadFlag()
    {
        SkipSeparators();
        if (_position < _text.Length)
        {
            char c = _text[_position];
            if (c == '0' || c == '1')
            {
                _position++;
                return c == '1';
            }
        }
        throw Error("expected an arc flag (0 or 1)");
    }

    private float ReadNumber()
    {
        SkipSeparators();
        int begin = _position;
        if (_position < _text.Length && (_text[_position] == '+' || _text[_position] == '-')) { _position++; }
        bool digits = false;
        while (_position < _text.Length && Char.IsAsciiDigit(_text[_position])) { _position++; digits = true; }
        if (_position < _text.Length && _text[_position] == '.')
        {
            _position++;
            while (_position < _text.Length && Char.IsAsciiDigit(_text[_position])) { _position++; digits = true; }
        }
        if (!digits)
        {
            _position = begin;
            throw Error("expected a number");
        }
        if (_position < _text.Length && (_text[_position] == 'e' || _text[_position] == 'E'))
        {
            int mark = _position;
            _position++;
            if (_position < _text.Length && (_text[_position] == '+' || _text[_position] == '-')) { _position++; }
            bool exponentDigits = false;
            while (_position < _text.Length && Char.IsAsciiDigit(_text[_position])) { _position++; exponentDigits = true; }
            if (!exponentDigits) { _position = mark; }
        }
        string token = _text.Substring(begin, _position - begin);
        if (!Single.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || !Single.IsFinite(value))
        {
            _position = begin;
            throw Error("expected a finite number");
        }
        return value;
    }

    private void SkipSeparators()
    {
        bool comma = false;
        while (_position < _text.Length)
        {
            char c = _text[_position];
            if (c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '\f') { _position++; continue; }
            if (c == ',' && !comma) { comma = true; _position++; continue; }
            break;
        }
    }

    private FormatException Error(string message)
        => new FormatException($"Invalid SVG path data at position {_position}: {message}.");
}
