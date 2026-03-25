using System.Collections.Generic;

public interface IParameterGenerator<T>
{
    ParameterResult<T> Generate(List<GestureEvent> events);
}
