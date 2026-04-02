{{!-- LLM_INJECTION_START: Models --}}
{{!--
  The LLM will generate one C# record per entity here.
  Expected output format per entity:

  public record {EntityName}(
      Guid Id,
      {Field1Type} {Field1Name},
      {Field2Type} {Field2Name},
      ...
      DateTime CreatedAt
  );
--}}
{{!-- LLM_INJECTION_END: Models --}}
